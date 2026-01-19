using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Editor.VisualElements;
using static world.anlabo.mdnailtool.Editor.Language.LanguageManager;
using Avatar = world.anlabo.mdnailtool.Editor.Entity.Avatar;
using Object = UnityEngine.Object;
using Newtonsoft.Json;
using world.anlabo.mdnailtool.Editor;
using world.anlabo.mdnailtool.Editor.Window.Domain;
using world.anlabo.mdnailtool.Editor.Window.Controllers;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Window
{
	public class MDNailToolWindow : EditorWindow
	{
		public static void ShowWindow()
		{
			MDNailToolWindow window = CreateWindow<MDNailToolWindow>();
			window.Show();
		}

		#region Constants & Fields

		private const string SCENE_PREVIEW_NAME = "[MDNailTool_Preview]";

		private LocalizedObjectField? _materialObjectField;
		private LocalizedObjectField? _avatarObjectField;
		private AvatarDropDowns? _avatarDropDowns;
		private AvatarSortDropdown? _avatarSortDropdown;
		private NailDesignSelect? _nailDesignSelect;
		private NailPreview? _nailPreview;
		private NailShapeDropDown? _nailShapeDropDown;
		private LocalizedDropDown? _nailMaterialDropDown;
		private LocalizedDropDown? _nailColorDropDown;

		private NailDesignDropDowns[]? _nailDesignDropDowns;

		private Toggle? _tglHandActive;
		private Toggle? _tglHandDetail;
		private Toggle? _tglFootActive;
		private Toggle? _tglFootDetail;

		private Toggle? _removeCurrentNail;
		private Toggle? _backup;
		private Toggle? _enableScenePreview;
		private Toggle? _forModularAvatar;
		private LocalizedButton? _execute;
		private LocalizedButton? _remove;

		private IVisualElementScheduledItem? _scenePreviewSchedule;
		private const int SCENE_PREVIEW_DEBOUNCE_MS = 150;

		private NailPreviewController? _nailPreviewController;

		private VisualElement? _handSelects;
		private VisualElement? _footSelects;

		private Label? _manualLink;
		private LocalizedLabel? _contactLink;

		#endregion

		public void SetAvatar(Shop shop, Avatar? avatar, AvatarVariation? variation)
		{
			this._avatarDropDowns?.SetValues(shop, avatar, variation);
		}

		public void CreateGUI()
		{
			this.PrepareOnCreateGUI();
			this.BuildRootUI();
			this.BindCoreFields();
			this.BindAvatarUI();
			this.BindNailUI();
			this.BindHandFootUI();
			this.BindOptionsUI();
			this.BindLinksUI();
			this.BindActions();
			this.PostInitSelection();
		}


		private void PrepareOnCreateGUI()
		{
			MDNailToolUsageStats.Migrate();
			INailProcessor.ClearPreviewMaterialCash();
			this.CleanupScenePreview();
		}
		private void BuildRootUI()
		{
			var uss = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(MDNailToolGuids.WindowUss);
			if (uss != null)
			{
				this.rootVisualElement.styleSheets.Add(uss);
			}

			var uxml = MDNailToolAssetLoader.LoadByGuid<VisualTreeAsset>(MDNailToolGuids.WindowUxml);
			if (uxml != null)
			{
				uxml.CloneTree(this.rootVisualElement);
			}
		}
		private void BindCoreFields()
		{
			this._materialObjectField = this.rootVisualElement.Q<LocalizedObjectField>("material-object");
			this._materialObjectField.RegisterValueChangedCallback(this.OnChangeMaterial);

			this._avatarObjectField = this.rootVisualElement.Q<LocalizedObjectField>("avatar-object");
			this._avatarObjectField.RegisterValueChangedCallback(this.OnChangeAvatar);
		}
		private void BindAvatarUI()
		{
			this._avatarDropDowns = this.rootVisualElement.Q<AvatarDropDowns>("avatar");
			this._avatarDropDowns.SearchButtonClicked += this.ShowAvatarSearchWindow;

			this._avatarDropDowns.RegisterCallback<ChangeEvent<string>>(_ =>
			{
				this.CleanupScenePreview();
				this.UpdatePreview();
				this.RequestScenePreviewUpdate();
			});

			this._avatarSortDropdown = this.rootVisualElement.Q<AvatarSortDropdown>("avatar-sort");
			this._avatarSortDropdown.Init();
			this._avatarSortDropdown.RegisterValueChangedCallback(this.OnChangeAvatarSort);
		}

		private void BindNailUI()
		{
			this._nailDesignSelect = this.rootVisualElement.Q<NailDesignSelect>("nail-select");
			this._nailDesignSelect.OnSelectNail += this.OnSelectNail;
			this._nailDesignSelect.OnSearchButtonClicked += this.ShowNailSearchWindow;

			this._nailPreview = this.rootVisualElement.Q<NailPreview>("nail-preview");
			this._nailPreviewController = new NailPreviewController(this._nailPreview);

			this._nailShapeDropDown = this.rootVisualElement.Q<NailShapeDropDown>("nail-shape");
			this._nailShapeDropDown.SetNailShape(GlobalSetting.LastUseShapeName);
			this._nailShapeDropDown.RegisterValueChangedCallback(this.OnChangeShapeDropDown);

			this._nailMaterialDropDown = this.rootVisualElement.Q<LocalizedDropDown>("nail-material");
			this._nailMaterialDropDown.RegisterValueChangedCallback(this.OnChangeNailMaterialDropDown);

			this._nailColorDropDown = this.rootVisualElement.Q<LocalizedDropDown>("nail-color");
			this._nailColorDropDown.RegisterValueChangedCallback(this.OnChangeNailColorDropDown);
		}

		private void BindHandFootUI()
		{
			this.SetupFootVisualElements();
			this.InitializeNailDesignDropDowns();
			this.InitializeHandFootControl();
		}
		private void BindOptionsUI()
		{
			this._removeCurrentNail = this.rootVisualElement.Q<Toggle>("remove-current-nail");
			this._removeCurrentNail.SetValueWithoutNotify(GlobalSetting.RemoveCurrentNail);
			this._removeCurrentNail.RegisterValueChangedCallback(OnChangeRemoveCurrentNail);

			this._backup = this.rootVisualElement.Q<Toggle>("backup");
			this._backup.SetValueWithoutNotify(GlobalSetting.Backup);
			this._backup.RegisterValueChangedCallback(OnChangeBackup);

			this._enableScenePreview = this.rootVisualElement.Q<Toggle>("enable-scene-preview");
			if (this._enableScenePreview != null)
			{
				this._enableScenePreview.SetValueWithoutNotify(GlobalSetting.EnableScenePreview);
				this._enableScenePreview.RegisterValueChangedCallback(this.OnChangeEnableScenePreview);
			}

			this._forModularAvatar = this.rootVisualElement.Q<Toggle>("for-modular-avatar");
			this._forModularAvatar.SetValueWithoutNotify(GlobalSetting.UseModularAvatar);
			this._forModularAvatar.RegisterValueChangedCallback(OnChangeForModularAvatar);
		}
		private void BindLinksUI()
		{
			this._manualLink = this.rootVisualElement.Q<Label>("link-manual");
			this._manualLink.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.manual")));

			this._contactLink = this.rootVisualElement.Q<LocalizedLabel>("link-contact");
			this._contactLink.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.contact")));

			this.rootVisualElement.Q<Label>("version").text = MDNailToolDefines.Version;
		}

		private void BindActions()
		{
			this._execute = this.rootVisualElement.Q<LocalizedButton>("execute");
			this._remove = this.rootVisualElement.Q<LocalizedButton>("remove");

			this._execute.clicked += this.OnExecute;
			this._remove.clicked += this.OnRemove;
		}

		private void PostInitSelection()
		{
			var nailDesignSelect = this._nailDesignSelect;
			if (nailDesignSelect != null && nailDesignSelect.FirstDesignName != null)
			{
				this.OnSelectNail(nailDesignSelect.FirstDesignName);
			}
			else
			{
				this.UpdatePreview();
				this.RequestScenePreviewUpdate();
			}

			if (Selection.activeGameObject != null)
			{
				VRCAvatarDescriptor? descriptor = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>();
				if (descriptor != null)
				{
					var avatarObjectField = this._avatarObjectField;
					if (avatarObjectField != null)
					{
						avatarObjectField.value = descriptor;
					}

					AvatarMatching avatarMatching = new(descriptor);
					(Shop shop, Entity.Avatar avatar, AvatarVariation variation)? variation = avatarMatching.Match();
					if (variation != null && this._avatarDropDowns != null)
					{
						this._avatarDropDowns.SetValues(variation.Value.shop, variation.Value.avatar, variation.Value.variation);
					}
				}
			}
		}


		private void SetupFootVisualElements()
		{
			var footSelects = this.rootVisualElement.Q<VisualElement>("foot-selects");
			this._footSelects = footSelects;

			if (footSelects == null) return;

			footSelects.Clear();

			var leftFootLabel = new LocalizedLabel { TextId = "window.left_foot", style = { marginBottom = 2, marginTop = 5 } };
			footSelects.Add(leftFootLabel);

			string[] leftToes = { "left-foot-thumb", "left-foot-index", "left-foot-middle", "left-foot-ring", "left-foot-little" };
			string[] toeLabels = { "window.toe.thumb", "window.toe.index", "window.toe.middle", "window.toe.ring", "window.toe.little" };

			for (int i = 0; i < 5; i++)
			{
				var dd = new NailDesignDropDowns { name = leftToes[i] };
				footSelects.Add(dd);

				var innerDropdown = dd.Q<DropdownField>("NailDesignDropDowns-DesignDropDown");
				if (innerDropdown is DropdownField ddf)
				{
					ddf.label = S(toeLabels[i]) ?? toeLabels[i];
				}
			}

			var rightFootLabel = new LocalizedLabel { TextId = "window.right_foot", style = { marginBottom = 2, marginTop = 10 } };
			footSelects.Add(rightFootLabel);

			string[] rightToes = { "right-foot-thumb", "right-foot-index", "right-foot-middle", "right-foot-ring", "right-foot-little" };
			for (int i = 0; i < 5; i++)
			{
				var dd = new NailDesignDropDowns { name = rightToes[i] };
				footSelects.Add(dd);

				var innerDropdown = dd.Q<DropdownField>("NailDesignDropDowns-DesignDropDown");
				if (innerDropdown is DropdownField ddf)
				{
					ddf.label = S(toeLabels[i]) ?? toeLabels[i];
				}
			}
		}

		private void InitializeNailDesignDropDowns()
		{
			this._nailDesignDropDowns = new[] {
				this.rootVisualElement.Q<NailDesignDropDowns>("left-thumb"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-index"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-middle"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-ring"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-little"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-thumb"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-index"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-middle"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-ring"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-little"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-foot-thumb"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-foot-index"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-foot-middle"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-foot-ring"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-foot-little"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-foot-thumb"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-foot-index"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-foot-middle"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-foot-ring"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-foot-little")
			};

			this._nailDesignDropDowns = this._nailDesignDropDowns.Where(d => d != null).ToArray();

			foreach (NailDesignDropDowns nailDesignDropDown in this._nailDesignDropDowns)
			{
				nailDesignDropDown.RegisterCallback<ChangeEvent<string?>>(this.OnChangeNailDesign);
			}
		}

		private void InitializeHandFootControl()
		{
			this._tglHandActive = this.rootVisualElement.Q<Toggle>("toggle-hand-active");
			this._tglHandDetail = this.rootVisualElement.Q<Toggle>("toggle-hand-detail");
			var lblHandActive = this.rootVisualElement.Q<Label>("label-hand-active");
			var lblHandDetail = this.rootVisualElement.Q<Label>("label-hand-detail");

			this._tglFootActive = this.rootVisualElement.Q<Toggle>("toggle-foot-active");
			this._tglFootDetail = this.rootVisualElement.Q<Toggle>("toggle-foot-detail");
			var lblFootActive = this.rootVisualElement.Q<Label>("label-foot-active");
			var lblFootDetail = this.rootVisualElement.Q<Label>("label-foot-detail");

			this._handSelects = this.rootVisualElement.Q<VisualElement>("hand-selects");
			if (this._footSelects == null) this._footSelects = this.rootVisualElement.Q<VisualElement>("foot-selects");

			if (_tglHandActive != null && _tglHandDetail != null)
			{
				_tglHandActive.SetValueWithoutNotify(MDNailToolPrefs.HandActive);
				_tglHandDetail.SetValueWithoutNotify(MDNailToolPrefs.HandDetail);

				lblHandActive?.RegisterCallback<ClickEvent>(_ => _tglHandActive.value = !_tglHandActive.value);
				lblHandDetail?.RegisterCallback<ClickEvent>(_ => _tglHandDetail.value = !_tglHandDetail.value);

				void UpdateHandVisiblity()
				{
					if (this._handSelects == null) return;
					bool isActive = _tglHandActive.value;
					bool isDetail = _tglHandDetail.value;

					this._handSelects.style.display = (isActive && isDetail) ? DisplayStyle.Flex : DisplayStyle.None;
					_tglHandDetail.SetEnabled(isActive);

					MDNailToolPrefs.HandActive = isActive;
					MDNailToolPrefs.HandDetail = isDetail;

					this.UpdatePreview();
					this.RequestScenePreviewUpdate();
				}

				_tglHandActive.RegisterValueChangedCallback(_ => UpdateHandVisiblity());
				_tglHandDetail.RegisterValueChangedCallback(_ => UpdateHandVisiblity());
				UpdateHandVisiblity();
			}

			if (_tglFootActive != null && _tglFootDetail != null)
			{
				_tglFootActive.SetValueWithoutNotify(GlobalSetting.UseFootNail);
				_tglFootDetail.SetValueWithoutNotify(MDNailToolPrefs.FootDetail);

				lblFootActive?.RegisterCallback<ClickEvent>(_ => _tglFootActive.value = !_tglFootActive.value);
				lblFootDetail?.RegisterCallback<ClickEvent>(_ => _tglFootDetail.value = !_tglFootDetail.value);

				void UpdateFootVisibility()
				{
					if (this._footSelects == null) return;
					bool isActive = _tglFootActive.value;
					bool isDetail = _tglFootDetail.value;

					this._footSelects.style.display = (isActive && isDetail) ? DisplayStyle.Flex : DisplayStyle.None;
					_tglFootDetail.SetEnabled(isActive);

					GlobalSetting.UseFootNail = isActive;
					MDNailToolPrefs.FootDetail = isDetail;

					this.UpdatePreview();
					this.RequestScenePreviewUpdate();
				}

				_tglFootActive.RegisterValueChangedCallback(_ => UpdateFootVisibility());
				_tglFootDetail.RegisterValueChangedCallback(_ => UpdateFootVisibility());
				UpdateFootVisibility();
			}
		}

		private void OnChangeAvatarSort(ChangeEvent<AvatarSortOrder> evt) { this._avatarDropDowns?.Sort(evt.newValue); }

		private void OnChangeAvatar(ChangeEvent<Object> evt)
		{
			if (evt.newValue is VRCAvatarDescriptor avatar)
			{
				AvatarMatching matching = new(avatar);
				(Shop shop, Entity.Avatar avatar, AvatarVariation variation)? result = matching.Match();
				if (result != null) this._avatarDropDowns!.SetValues(result.Value.shop, result.Value.avatar, result.Value.variation);

				this.CleanupScenePreview();
				this.UpdatePreview();
				this.RequestScenePreviewUpdate();
			}
		}

		private void OnDestroy()
		{
			INailProcessor.ClearPreviewMaterialCash();
			this.CleanupScenePreview();
		}

		private world.anlabo.mdnailtool.Editor.Window.Controllers.MDNailScenePreviewController? _scenePreviewController;


		private void OnExecute()
		{
			this.CleanupScenePreview();

			AssetDatabase.StartAssetEditing();
			try
			{
				VRCAvatarDescriptor? avatar = this._avatarObjectField!.value as VRCAvatarDescriptor;
				if (avatar == null)
				{
					EditorUtility.DisplayDialog(S("dialog.error"), S("dialog.error.select_target_avatar"), "OK");
					return;
				}

				AvatarVariation? avatarVariationData = this._avatarDropDowns!.GetSelectedAvatarVariation();
				GameObject? prefab = this._avatarDropDowns!.GetSelectedPrefab();
				string? nailShapeName = this._nailShapeDropDown!.value;

				if (avatarVariationData == null || prefab == null || nailShapeName == null)
				{
					Debug.LogError("Required settings are missing.");
					return;
				}

				(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();

				Mesh?[]? selectedMeshes = this._nailShapeDropDown!.GetSelectedShapeMeshes();
				Mesh?[]? overrideMesh = (this._tglHandActive?.value ?? true) ? selectedMeshes : new Mesh?[10];

				Material? directMaterial = this._materialObjectField!.value as Material;

				NailSetupProcessor processor = new(avatar, avatarVariationData, prefab, designAndVariationNames, nailShapeName)
				{
					AvatarName = this._avatarDropDowns.GetAvatarName(),
					OverrideMesh = overrideMesh,
					UseFootNail = this._tglFootActive!.value,
					RemoveCurrentNail = this._removeCurrentNail!.value,
					GenerateMaterial = directMaterial == null,  // 直接指定時は生成OFF
					Backup = this._backup!.value,
					ForModularAvatar = this._forModularAvatar!.value,
					OverrideMaterial = directMaterial
				};

				processor.Process();

				if (!this._tglHandActive!.value) this.RemoveHandNailObjects(avatar);
				if (!this._tglFootActive!.value) this.RemoveFootNailObjects(avatar);

				string avatarKey = this._avatarDropDowns!.GetAvatarKey();
				MDNailToolUsageStats.Update(designAndVariationNames, avatarKey);
				this._nailDesignSelect!.Init();

				EditorUtility.DisplayDialog(S("dialog.finished"), S("dialog.finished.success_attach_nail"), "OK");
			}
			catch (Exception e)
			{
				Debug.LogError(e);
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}
		}

		private void RemoveHandNailObjects(VRCAvatarDescriptor avatar)
		{
			foreach (string nailName in MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST)
			{
				var targets = avatar.transform.GetComponentsInChildren<Transform>(true)
					.Where(t => t.name.Contains(nailName)).ToArray();
				foreach (var t in targets) if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
			}
		}

		private void RemoveFootNailObjects(VRCAvatarDescriptor avatar)
		{
			var targetNames = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
				.Concat(MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST);
			foreach (string nailName in targetNames)
			{
				var targets = avatar.transform.GetComponentsInChildren<Transform>(true)
					.Where(t => t.name.Contains(nailName)).ToArray();
				foreach (var t in targets) if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
			}
		}

		private void OnRemove()
		{
			this.CleanupScenePreview();
			VRCAvatarDescriptor? avatar = this._avatarObjectField!.value as VRCAvatarDescriptor;
			if (avatar == null)
			{
				EditorUtility.DisplayDialog(S("dialog.error"), S("dialog.error.select_target_avatar"), "OK");
				return;
			}
			AvatarVariation? avatarVariationData = this._avatarDropDowns!.GetSelectedAvatarVariation();
			if (avatarVariationData != null) NailSetupProcessor.RemoveNail(avatar, avatarVariationData.BoneMappingOverride);
		}

		private void OnSelectNail(string designName)
		{
			using DBNailDesign dbNailDesign = new();
			NailDesign? design = dbNailDesign.FindNailDesignByDesignName(designName);
			if (design?.DesignName == null) return;
			INailProcessor nailProcessor = INailProcessor.CreateNailDesign(design.DesignName);

			List<string> materialPopupElements = design.MaterialVariation != null ?
				design.MaterialVariation.Where(pair => nailProcessor.IsInstalledMaterialVariation(pair.Value.MaterialName)).Select(pair => pair.Value.MaterialName).ToList() : new List<string> { "" };
			string materialValue = materialPopupElements.FirstOrDefault() ?? "";

			List<string> colorPopupElements = design.ColorVariation
				.Where(pair => nailProcessor.IsInstalledColorVariation(materialValue, pair.Value.ColorName)).Select(pair => pair.Value.ColorName).ToList();
			string colorValue = colorPopupElements.FirstOrDefault() ?? "";

			foreach (NailDesignDropDowns nailDesignDropDowns in this._nailDesignDropDowns!)
			{
				nailDesignDropDowns.SetValue(designName, materialValue, materialPopupElements, colorValue, colorPopupElements);
			}

			this._nailMaterialDropDown!.choices = materialPopupElements;
			this._nailMaterialDropDown!.SetValueWithoutNotify(materialValue);
			this._nailColorDropDown!.choices = colorPopupElements;
			this._nailColorDropDown!.SetValueWithoutNotify(colorValue);

			this.UpdateNailShapeFilter(nailProcessor);
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
		}

		private void OnChangeMaterial(ChangeEvent<Object?> evt) { this.UpdateNailShapeFilter(); this.UpdatePreview(); this.RequestScenePreviewUpdate(); }
		private void OnChangeShapeDropDown(ChangeEvent<string> evt) { GlobalSetting.LastUseShapeName = evt.newValue; this.UpdatePreview(); this.RequestScenePreviewUpdate(); }
		private void OnChangeNailMaterialDropDown(ChangeEvent<string?> evt)
		{
			if (evt.newValue == null) return;
			foreach (var dd in this._nailDesignDropDowns!) dd.SetMaterialValue(evt.newValue);
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
		}
		private void OnChangeNailColorDropDown(ChangeEvent<string?> evt)
		{
			if (evt.newValue == null) return;
			foreach (var dd in this._nailDesignDropDowns!) dd.SetColorValue(evt.newValue);
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
		}
		private void OnChangeNailDesign(ChangeEvent<string?> evt)
		{
			if (evt.target is DropdownField { name: "NailDesignDropDowns-DesignDropDown" }) this.UpdateNailShapeFilter();
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
		}

		private void UpdateNailShapeFilter(INailProcessor? processor = null)
		{
			if (this._materialObjectField!.value != null) { this._nailShapeDropDown!.SetFilter(_ => true); return; }
			if (processor != null) { this._nailShapeDropDown!.SetFilter(processor.IsSupportedNailShape); return; }

			HashSet<string> designNameSet = this._nailDesignDropDowns!.Select(downs => downs.GetSelectedDesignName()).ToHashSet();
			using DBNailDesign dbNailDesign = new();
			List<INailProcessor> processors = designNameSet.Select(INailProcessor.CreateNailDesign).ToList();
			this._nailShapeDropDown!.SetFilter(shapeName => processors.All(p => p.IsSupportedNailShape(shapeName)));
		}

		private void UpdatePreview()
		{
			string nailShapeName = this._nailShapeDropDown?.value ?? GlobalSetting.LastUseShapeName ?? "oval";
			if (string.IsNullOrEmpty(nailShapeName)) nailShapeName = "oval";
			if (string.IsNullOrEmpty(nailShapeName)) return;

			Mesh?[]? overrideMeshes = this._nailShapeDropDown?.GetSelectedShapeMeshes();
			if (overrideMeshes == null) return;

			this._nailPreviewController?.ChangeNailShape(overrideMeshes);
			this._nailPreviewController?.ChangeFootNailMesh(nailShapeName);

			bool isHandActive = this._tglHandActive?.value ?? true;
			bool isFootActive = this._tglFootActive?.value ?? false;
			this._nailPreviewController?.UpdateVisibility(isHandActive, isFootActive);

			(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();
			Material? directMaterial = this._materialObjectField?.value as Material;

			this._nailPreviewController?.ChangeNailMaterial(designAndVariationNames, nailShapeName, directMaterial);
			this._nailPreviewController?.ChangeAdditionalObjects(designAndVariationNames, nailShapeName);
		}

		private void RequestScenePreviewUpdate()
		{
			if (!GlobalSetting.EnableScenePreview) return;

			this._scenePreviewSchedule?.Pause();
			this._scenePreviewSchedule = this.rootVisualElement.schedule
				.Execute(() => this.UpdateScenePreview(immediate: true))
				.StartingIn(SCENE_PREVIEW_DEBOUNCE_MS);
		}

		private void UpdateScenePreview(bool immediate)
		{
			if (!GlobalSetting.EnableScenePreview) return;

			var avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			if (avatar == null) return;

			var prefab = this._avatarDropDowns?.GetSelectedPrefab();
			if (prefab == null) return;

			string nailShapeName = this._nailShapeDropDown?.value ?? GlobalSetting.LastUseShapeName ?? "oval";
			if (string.IsNullOrEmpty(nailShapeName)) nailShapeName = "oval";
			if (string.IsNullOrEmpty(nailShapeName)) return;

			Mesh?[]? overrideMeshes = this._nailShapeDropDown?.GetSelectedShapeMeshes();
			if (overrideMeshes == null) overrideMeshes = new Mesh?[0];

			bool isHandActive = this._tglHandActive?.value ?? true;
			bool isFootActive = this._tglFootActive?.value ?? false;

			(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();
			Material? directMaterial = this._materialObjectField?.value as Material;

			this._scenePreviewController ??= new MDNailScenePreviewController(SCENE_PREVIEW_NAME);
			this._scenePreviewController.Update(
				avatar,
				prefab,
				overrideMeshes,
				nailShapeName,
				isHandActive,
				isFootActive,
				designAndVariationNames,
				directMaterial
			);
		}


		private void CleanupScenePreview()
		{
			this._scenePreviewController?.Cleanup(this._avatarObjectField?.value as VRCAvatarDescriptor);
		}


		private (INailProcessor, string, string)[] GetNailProcessors()
		{
			return MDNailSelectionBuilder.Build(
				this._nailDesignDropDowns!,
				this._tglHandActive?.value ?? true,
				this._tglHandDetail?.value ?? false,
				this._tglFootActive?.value ?? false,
				this._tglFootDetail?.value ?? false
			);
		}

		private static void OnChangeRemoveCurrentNail(ChangeEvent<bool> evt) { GlobalSetting.RemoveCurrentNail = evt.newValue; }
		private static void OnChangeBackup(ChangeEvent<bool> evt) { GlobalSetting.Backup = evt.newValue; }
		private static void OnChangeForModularAvatar(ChangeEvent<bool> evt) { GlobalSetting.UseModularAvatar = evt.newValue; }
		private void ShowAvatarSearchWindow() { SearchAvatarWindow.ShowWindow(this); }
		private void ShowNailSearchWindow() { SearchNailDesignWindow.ShowWindow(this); }
		public void SelectNailFromSearch(string designName) { this.OnSelectNail(designName); }

		private void OnChangeEnableScenePreview(ChangeEvent<bool> evt)
		{
			GlobalSetting.EnableScenePreview = evt.newValue;

			var avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			if (avatar != null)
			{
				this._scenePreviewController ??= new MDNailScenePreviewController(SCENE_PREVIEW_NAME);
				this._scenePreviewController.SetScenePreviewActive(avatar, evt.newValue);
			}

			if (evt.newValue) this.UpdateScenePreview(immediate: true);
		}

	}
}
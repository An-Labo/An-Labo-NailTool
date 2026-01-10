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

		private const string GUID = "f44afb5feae822a4b9308df804788d69";
		private const string USS_GUID = "518510ce88e1451e8f8f06ab4add9daf";
		
		private const string SCENE_PREVIEW_NAME = "[MDNailTool_Preview]";

		private const string PREF_KEY_HAND_ACTIVE = "MDNailTool_HandActive";
		private const string PREF_KEY_HAND_DETAIL = "MDNailTool_HandDetail";
		private const string PREF_KEY_FOOT_DETAIL = "MDNailTool_FootDetail";

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
		private Toggle? _forModularAvatar;

		private LocalizedButton? _execute;
		private LocalizedButton? _remove;

		private NailPreviewController? _nailPreviewController;

		private VisualElement? _handSelects;
		private VisualElement? _footSelects;

		private Label? _manualLink;
		private LocalizedLabel? _contactLink;
		
		private GameObject? _scenePreviewObject;

		#endregion

		public void SetAvatar(Shop shop, Avatar? avatar, AvatarVariation? variation)
		{
			this._avatarDropDowns?.SetValues(shop, avatar, variation);
		}

		private void CreateGUI()
		{
			this.MigrateUsageStats();
			INailProcessor.ClearPreviewMaterialCash();
			this.CleanupScenePreview();

			using DBShop _dbShop = new();
			using DBNailDesign _dbNailDesign = new();

			string ussPath = AssetDatabase.GUIDToAssetPath(USS_GUID);
			if (!string.IsNullOrEmpty(ussPath))
			{
				StyleSheet uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
				this.rootVisualElement.styleSheets.Add(uss);
			}

			string uxmlPath = AssetDatabase.GUIDToAssetPath(GUID);
			VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
			uxml.CloneTree(this.rootVisualElement);

			this._materialObjectField = this.rootVisualElement.Q<LocalizedObjectField>("material-object");
			this._materialObjectField.RegisterValueChangedCallback(this.OnChangeMaterial);

			this._avatarObjectField = this.rootVisualElement.Q<LocalizedObjectField>("avatar-object");
			this._avatarObjectField.RegisterValueChangedCallback(this.OnChangeAvatar);

			this._avatarDropDowns = this.rootVisualElement.Q<AvatarDropDowns>("avatar");
			this._avatarDropDowns.SearchButtonClicked += this.ShowAvatarSearchWindow;
			
			this._avatarDropDowns.RegisterCallback<ChangeEvent<string>>(_ => {
				this.CleanupScenePreview();
				this.UpdatePreview();
			});

			this._avatarSortDropdown = this.rootVisualElement.Q<AvatarSortDropdown>("avatar-sort");
			this._avatarSortDropdown.Init();
			this._avatarSortDropdown.RegisterValueChangedCallback(this.OnChangeAvatarSort);

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

			this.SetupFootVisualElements();

			this.InitializeNailDesignDropDowns();

			this.InitializeHandFootControl();

			this._removeCurrentNail = this.rootVisualElement.Q<Toggle>("remove-current-nail");
			this._removeCurrentNail.SetValueWithoutNotify(GlobalSetting.RemoveCurrentNail);
			this._removeCurrentNail.RegisterValueChangedCallback(OnChangeRemoveCurrentNail);

			this._backup = this.rootVisualElement.Q<Toggle>("backup");
			this._backup.SetValueWithoutNotify(GlobalSetting.Backup);
			this._backup.RegisterValueChangedCallback(OnChangeBackup);

			this._forModularAvatar = this.rootVisualElement.Q<Toggle>("for-modular-avatar");
			this._forModularAvatar.SetValueWithoutNotify(GlobalSetting.UseModularAvatar);
			this._forModularAvatar.RegisterValueChangedCallback(OnChangeForModularAvatar);

			this._execute = this.rootVisualElement.Q<LocalizedButton>("execute");
			this._remove = this.rootVisualElement.Q<LocalizedButton>("remove");

			this._manualLink = this.rootVisualElement.Q<Label>("link-manual");
			this._manualLink.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.manual")));

			this._contactLink = this.rootVisualElement.Q<LocalizedLabel>("link-contact");
			this._contactLink.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.contact")));

			this.rootVisualElement.Q<Label>("version").text = MDNailToolDefines.Version;

			this._execute.clicked += this.OnExecute;
			this._remove.clicked += this.OnRemove;

			if (this._nailDesignSelect.FirstDesignName != null)
			{
				this.OnSelectNail(this._nailDesignSelect.FirstDesignName);
			}
			else
			{
				this.UpdatePreview();
			}

			if (Selection.activeGameObject != null)
			{
				VRCAvatarDescriptor? descriptor = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>();
				if (descriptor != null)
				{
					this._avatarObjectField.value = descriptor;
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
			this._footSelects = this.rootVisualElement.Q<VisualElement>("foot-selects");
			if (this._footSelects != null)
			{
				this._footSelects.Clear();

				var leftFootLabel = new LocalizedLabel { TextId = "window.left_foot", style = { marginBottom = 2, marginTop = 5 } };
    			this._footSelects.Add(leftFootLabel);

				string[] leftToes = { "left-foot-thumb", "left-foot-index", "left-foot-middle", "left-foot-ring", "left-foot-little" };
				string[] toeLabels = { "window.thumb", "window.index_finger", "window.middle_finger", "window.ring_finger", "window.little_finger" };

				for (int i = 0; i < 5; i++)
				{
					var dd = new NailDesignDropDowns { name = leftToes[i] };
					this._footSelects.Add(dd);
					var innerDropdown = dd.Q<DropdownField>("NailDesignDropDowns-DesignDropDown");
					if (innerDropdown != null) innerDropdown.label = S(toeLabels[i]) ?? toeLabels[i];
				}

				var rightFootLabel = new LocalizedLabel { TextId = "window.right_foot", style = { marginBottom = 2, marginTop = 10 } };
    			this._footSelects.Add(rightFootLabel);

				string[] rightToes = { "right-foot-thumb", "right-foot-index", "right-foot-middle", "right-foot-ring", "right-foot-little" };
				for (int i = 0; i < 5; i++)
				{
					var dd = new NailDesignDropDowns { name = rightToes[i] };
					this._footSelects.Add(dd);
					var innerDropdown = dd.Q<DropdownField>("NailDesignDropDowns-DesignDropDown");
					if (innerDropdown != null) innerDropdown.label = S(toeLabels[i]) ?? toeLabels[i];
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
			if(this._footSelects == null) this._footSelects = this.rootVisualElement.Q<VisualElement>("foot-selects");

			if (_tglHandActive != null && _tglHandDetail != null)
			{
				_tglHandActive.SetValueWithoutNotify(EditorPrefs.GetBool(PREF_KEY_HAND_ACTIVE, true));
				_tglHandDetail.SetValueWithoutNotify(EditorPrefs.GetBool(PREF_KEY_HAND_DETAIL, false));

				lblHandActive?.RegisterCallback<ClickEvent>(_ => _tglHandActive.value = !_tglHandActive.value);
				lblHandDetail?.RegisterCallback<ClickEvent>(_ => _tglHandDetail.value = !_tglHandDetail.value);

				void UpdateHandVisiblity()
				{
					if (this._handSelects == null) return;
					bool isActive = _tglHandActive.value;
					bool isDetail = _tglHandDetail.value;

					this._handSelects.style.display = (isActive && isDetail) ? DisplayStyle.Flex : DisplayStyle.None;
					_tglHandDetail.SetEnabled(isActive);

					EditorPrefs.SetBool(PREF_KEY_HAND_ACTIVE, isActive);
					EditorPrefs.SetBool(PREF_KEY_HAND_DETAIL, isDetail);

					this.UpdatePreview();
				}

				_tglHandActive.RegisterValueChangedCallback(_ => UpdateHandVisiblity());
				_tglHandDetail.RegisterValueChangedCallback(_ => UpdateHandVisiblity());
				UpdateHandVisiblity();
			}

			if (_tglFootActive != null && _tglFootDetail != null)
			{
				_tglFootActive.SetValueWithoutNotify(GlobalSetting.UseFootNail);
				_tglFootDetail.SetValueWithoutNotify(EditorPrefs.GetBool(PREF_KEY_FOOT_DETAIL, false));

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
					EditorPrefs.SetBool(PREF_KEY_FOOT_DETAIL, isDetail);

					this.UpdatePreview();
				}

				_tglFootActive.RegisterValueChangedCallback(_ => UpdateFootVisibility());
				_tglFootDetail.RegisterValueChangedCallback(_ => UpdateFootVisibility());
				UpdateFootVisibility();
			}
		}

		private void OnChangeAvatarSort(ChangeEvent<AvatarSortOrder> evt) { this._avatarDropDowns?.Sort(evt.newValue); }
		
		private void OnChangeAvatar(ChangeEvent<Object> evt) { 
			if (evt.newValue is VRCAvatarDescriptor avatar) {
				AvatarMatching matching = new(avatar);
				(Shop shop, Entity.Avatar avatar, AvatarVariation variation)? result = matching.Match();
				if (result != null) this._avatarDropDowns!.SetValues(result.Value.shop, result.Value.avatar, result.Value.variation);
				
				this.CleanupScenePreview();
				this.UpdatePreview();
			}
		}

		private void OnDestroy() 
		{ 
			INailProcessor.ClearPreviewMaterialCash();
			this.CleanupScenePreview(); 
		}

		private void OnExecute()
		{
			this.CleanupScenePreview();
			
			AssetDatabase.StartAssetEditing();
			try
			{
				VRCAvatarDescriptor? avatar = this._avatarObjectField!.value as VRCAvatarDescriptor;
				if (avatar == null) {
					EditorUtility.DisplayDialog(S("dialog.error"), S("dialog.error.select_target_avatar"), "OK");
					return;
				}

				AvatarVariation? avatarVariationData = this._avatarDropDowns!.GetSelectedAvatarVariation();
				GameObject? prefab = this._avatarDropDowns!.GetSelectedPrefab();
				string? nailShapeName = this._nailShapeDropDown!.value;

				if (avatarVariationData == null || prefab == null || nailShapeName == null) {
					Debug.LogError("Required settings are missing.");
					return;
				}

				(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();
				
				Mesh?[]? selectedMeshes = this._nailShapeDropDown!.GetSelectedShapeMeshes();
				Mesh?[]? overrideMesh = (this._tglHandActive?.value ?? true) ? selectedMeshes : new Mesh?[10];

				NailSetupProcessor processor = new(avatar, avatarVariationData, prefab, designAndVariationNames, nailShapeName)
				{
					AvatarName = this._avatarDropDowns.GetAvatarName(),
					OverrideMesh = overrideMesh,
					UseFootNail = this._tglFootActive!.value,
					RemoveCurrentNail = this._removeCurrentNail!.value,
					GenerateMaterial = true,
					Backup = this._backup!.value,
					ForModularAvatar = this._forModularAvatar!.value
				};

				processor.Process();

				if (!this._tglHandActive!.value) this.RemoveHandNailObjects(avatar);
				if (!this._tglFootActive!.value) this.RemoveFootNailObjects(avatar);

				this.UpdateUsageStats(designAndVariationNames);
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

		private void UpdateUsageStats((INailProcessor, string, string)[] designAndVariationNames)
		{
			Dictionary<string, DateTime> lastUsedTimes = GlobalSetting.DesignLastUsedTimes;
			Dictionary<string, int> designUsedCounts = GlobalSetting.DesignUseCount;
			Dictionary<string, int> avatarUseCount = GlobalSetting.AvatarUseCount;

			HashSet<string> uniqueDesignNames = new();
			foreach ((INailProcessor nailProcessor, string _, string _) in designAndVariationNames)
			{
				if (nailProcessor != null && !string.IsNullOrEmpty(nailProcessor.DesignName))
				{
					uniqueDesignNames.Add(nailProcessor.DesignName);
				}
			}

			foreach (string dName in uniqueDesignNames)
			{
				lastUsedTimes[dName] = DateTime.Now;
				designUsedCounts[dName] = designUsedCounts.GetValueOrDefault(dName, 0) + 1;
			}

			string avatarKey = this._avatarDropDowns!.GetAvatarKey();
			avatarUseCount[avatarKey] = avatarUseCount.GetValueOrDefault(avatarKey, 0) + 1;

			GlobalSetting.DesignLastUsedTimes = lastUsedTimes;
			GlobalSetting.DesignUseCount = designUsedCounts;
			GlobalSetting.AvatarUseCount = avatarUseCount;
		}

		private void OnRemove()
		{
			this.CleanupScenePreview(); 
			VRCAvatarDescriptor? avatar = this._avatarObjectField!.value as VRCAvatarDescriptor;
			if (avatar == null) {
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
		}

		private void OnChangeMaterial(ChangeEvent<Object?> evt) { this.UpdateNailShapeFilter(); this.UpdatePreview(); }
		private void OnChangeShapeDropDown(ChangeEvent<string> evt) { GlobalSetting.LastUseShapeName = evt.newValue; this.UpdatePreview(); }
		private void OnChangeNailMaterialDropDown(ChangeEvent<string?> evt) {
			if (evt.newValue == null) return;
			foreach (var dd in this._nailDesignDropDowns!) dd.SetMaterialValue(evt.newValue);
			this.UpdatePreview();
		}
		private void OnChangeNailColorDropDown(ChangeEvent<string?> evt) {
			if (evt.newValue == null) return;
			foreach (var dd in this._nailDesignDropDowns!) dd.SetColorValue(evt.newValue);
			this.UpdatePreview();
		}
		private void OnChangeNailDesign(ChangeEvent<string?> evt) {
			if (evt.target is DropdownField { name: "NailDesignDropDowns-DesignDropDown" }) this.UpdateNailShapeFilter();
			this.UpdatePreview();
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
			string? nailShapeName = this._nailShapeDropDown!.value;
			if (nailShapeName == null) return;
			Mesh?[]? overrideMeshes = this._nailShapeDropDown!.GetSelectedShapeMeshes();
			if (overrideMeshes == null) return;

			this._nailPreviewController!.ChangeNailShape(overrideMeshes);
			this._nailPreviewController!.ChangeFootNailMesh(this._nailShapeDropDown!.value);

			bool isHandActive = this._tglHandActive?.value ?? true;
			bool isFootActive = this._tglFootActive?.value ?? false;
			this._nailPreviewController.UpdateVisibility(isHandActive, isFootActive);

			(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();

			this._nailPreviewController.ChangeNailMaterial(designAndVariationNames, nailShapeName);
			this._nailPreviewController.ChangeAdditionalObjects(designAndVariationNames, nailShapeName);

			this.UpdateScenePreview(overrideMeshes, nailShapeName, isHandActive, isFootActive, designAndVariationNames);
		}

		private void UpdateScenePreview(Mesh?[] overrideMeshes, string nailShapeName, bool isHandActive, bool isFootActive, (INailProcessor, string, string)[] designAndVariationNames)
		{
			VRCAvatarDescriptor? avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			if (avatar == null) return;

			if (this._scenePreviewObject == null)
			{
				var existing = avatar.transform.Find(SCENE_PREVIEW_NAME);
				if (existing != null)
				{
					this._scenePreviewObject = existing.gameObject;
				}
				else
				{
					GameObject? prefab = this._avatarDropDowns?.GetSelectedPrefab();
					if (prefab == null) return;

					this._scenePreviewObject = Instantiate(prefab);
					this._scenePreviewObject.name = SCENE_PREVIEW_NAME;
					this._scenePreviewObject.transform.SetParent(avatar.transform, false);
					this._scenePreviewObject.hideFlags = HideFlags.DontSave; 
				}
			}

			if (this._scenePreviewObject == null) return;

			var allTransforms = this._scenePreviewObject.GetComponentsInChildren<Transform>(true);
			Transform? FindByName(string name) => allTransforms.FirstOrDefault(t => t.name.Contains(name));

			var hands = MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST.Select(FindByName).ToArray();
			foreach (var t in hands) if(t != null) t.gameObject.SetActive(isHandActive);

			var feet = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
				.Concat(MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST)
				.Select(name => FindByName(name)).ToArray();
			foreach (var t in feet) if(t != null) t.gameObject.SetActive(isFootActive);

			if (isHandActive && overrideMeshes.Length > 0)
			{
				NailSetupUtil.ReplaceHandsNailMesh(hands, overrideMeshes);
			}

			if (isFootActive)
			{
				var leftFeet = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST.Select(FindByName).ToArray();
				var rightFeet = MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST.Select(FindByName).ToArray();
				NailSetupUtil.ReplaceFootNailMesh(leftFeet, rightFeet, nailShapeName);
			}

			var pLeftFeet = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST.Select(FindByName);
			var pRightFeet = MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST.Select(FindByName);
			
			NailSetupUtil.ReplaceNailMaterial(hands, pLeftFeet, pRightFeet, designAndVariationNames, nailShapeName, true, true);
		}

		private void CleanupScenePreview()
		{
			if (this._scenePreviewObject != null)
			{
				DestroyImmediate(this._scenePreviewObject);
				this._scenePreviewObject = null;
			}
			
			VRCAvatarDescriptor? avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			if (avatar != null)
			{
				var existing = avatar.transform.Find(SCENE_PREVIEW_NAME);
				if (existing != null) DestroyImmediate(existing.gameObject);
			}
		}

		private (INailProcessor, string, string)[] GetNailProcessors()
		{
			(string d, string m, string c)[] allSelections = this._nailDesignDropDowns!
				.Select(dropDowns => dropDowns.GetSelectedDesignAndVariationName())
				.ToArray();

			List<(string d, string m, string c)> finalSelectionList = new();
			var emptyDummy = ("", "", "");
			var globalMasterSource = allSelections.Length > 0 ? allSelections[0] : emptyDummy;

			if (this._tglHandActive?.value ?? true) 
			{
				bool isHandDetail = this._tglHandDetail?.value ?? false;
				var handSource = allSelections[0]; 
				for (int i = 0; i < 10; i++) finalSelectionList.Add(isHandDetail ? allSelections[i] : handSource);
			}
			else
			{
				for (int i = 0; i < 10; i++) finalSelectionList.Add(emptyDummy);
			}

			if (this._tglFootActive?.value ?? false) 
			{
				bool isFootDetail = this._tglFootDetail?.value ?? false;
				for (int i = 10; i < 20; i++) finalSelectionList.Add(isFootDetail ? allSelections[i] : globalMasterSource);
			}
			else
			{
				for (int i = 0; i < 10; i++) finalSelectionList.Add(emptyDummy);
			}

			Dictionary<string, INailProcessor> designDictionary = new();
			return finalSelectionList.Select(tuple => {
				if (string.IsNullOrEmpty(tuple.d)) return (null!, "", "");
				if (designDictionary.TryGetValue(tuple.d, out INailProcessor nailDesign)) return (nailDesign, tuple.m, tuple.c);
				INailProcessor newProcessor = INailProcessor.CreateNailDesign(tuple.d);
				designDictionary.Add(tuple.d, newProcessor);
				return (newProcessor, tuple.m, tuple.c);
			}).ToArray();
		}

		private static void OnChangeRemoveCurrentNail(ChangeEvent<bool> evt) { GlobalSetting.RemoveCurrentNail = evt.newValue; }
		private static void OnChangeBackup(ChangeEvent<bool> evt) { GlobalSetting.Backup = evt.newValue; }
		private static void OnChangeForModularAvatar(ChangeEvent<bool> evt) { GlobalSetting.UseModularAvatar = evt.newValue; }
		private void ShowAvatarSearchWindow() { SearchAvatarWindow.ShowWindow(this); }
		private void ShowNailSearchWindow() { SearchNailDesignWindow.ShowWindow(this); }
		public void SelectNailFromSearch(string designName) { this.OnSelectNail(designName); }

		private void MigrateUsageStats()
		{
			const string OLD_DESIGN_KEY = "world.anlabo.mdnailtool.design_use_count";
			if (!EditorPrefs.HasKey(OLD_DESIGN_KEY)) return;
			var oldDesignCounts = JsonConvert.DeserializeObject<Dictionary<string, int>>(EditorPrefs.GetString(OLD_DESIGN_KEY));
			if (oldDesignCounts != null) {
				var migrated = new Dictionary<string, int>();
				foreach (var kvp in oldDesignCounts) migrated[kvp.Key] = Mathf.CeilToInt(kvp.Value / 24.0f);
				GlobalSetting.DesignUseCount = migrated;
			}
			EditorPrefs.DeleteKey(OLD_DESIGN_KEY);
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.avatar_use_count");
		}
	}
}
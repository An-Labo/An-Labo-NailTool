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

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Window {
	public class MDNailToolWindow : EditorWindow {
		public static void ShowWindow() {
			MDNailToolWindow window = CreateWindow<MDNailToolWindow>();
			window.Show();
		}

		private const string GUID = "f44afb5feae822a4b9308df804788d69";
		private const string USS_GUID = "518510ce88e1451e8f8f06ab4add9daf";

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

		private Toggle? _setPreFinger;
		private Toggle? _useFootNail;
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

		public void SetAvatar(Shop shop, Avatar? avatar, AvatarVariation? variation) {
			this._avatarDropDowns?.SetValues(shop, avatar, variation);
		}


		private void CreateGUI() {
			this.MigrateUsageStats();
			INailProcessor.ClearPreviewMaterialCash();

			using DBShop _dbShop = new();
			using DBNailDesign _dbNailDesign = new();

			string ussPath = AssetDatabase.GUIDToAssetPath(USS_GUID);
			StyleSheet uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
			this.rootVisualElement.styleSheets.Add(uss);

			string uxmlPath = AssetDatabase.GUIDToAssetPath(GUID);
			VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
			uxml.CloneTree(this.rootVisualElement);

			this._materialObjectField = this.rootVisualElement.Q<LocalizedObjectField>("material-object");
			this._materialObjectField.RegisterValueChangedCallback(this.OnChangeMaterial);
			this._avatarObjectField = this.rootVisualElement.Q<LocalizedObjectField>("avatar-object");
			this._avatarObjectField.RegisterValueChangedCallback(this.OnChangeAvatar);
			this._avatarDropDowns = this.rootVisualElement.Q<AvatarDropDowns>("avatar");
			this._avatarDropDowns.SearchButtonClicked += this.ShowAvatarSearchWindow;
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
				this.rootVisualElement.Q<NailDesignDropDowns>("left-foot"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-foot")
			};

			foreach (NailDesignDropDowns nailDesignDropDown in this._nailDesignDropDowns) {
				nailDesignDropDown.RegisterCallback<ChangeEvent<string?>>(this.OnChangeNailDesign);
			}


			this._setPreFinger = this.rootVisualElement.Q<Toggle>("set-per-finger");
			this._setPreFinger.RegisterValueChangedCallback(this.OnChangeSetPreFinger);
			this._useFootNail = this.rootVisualElement.Q<Toggle>("use-foot-nail");
			this._useFootNail.SetValueWithoutNotify(GlobalSetting.UseFootNail);
			this._useFootNail.RegisterValueChangedCallback(this.OnChangeUseFootNail);


			this._handSelects = this.rootVisualElement.Q<VisualElement>("hand-selects");
			this._handSelects.style.display = this._setPreFinger.value ? DisplayStyle.Flex : DisplayStyle.None;
			this._footSelects = this.rootVisualElement.Q<VisualElement>("foot-selects");
			this._footSelects.style.display = this._useFootNail.value ? DisplayStyle.Flex : DisplayStyle.None;

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
			this._manualLink.RegisterCallback<ClickEvent>(_ => {
				Application.OpenURL(S("link.manual"));
			});
			this._contactLink = this.rootVisualElement.Q<LocalizedLabel>("link-contact");
			this._contactLink.RegisterCallback<ClickEvent>(_ => {
				Application.OpenURL(S("link.contact"));
			});

			this.rootVisualElement.Q<Label>("version").text = MDNailToolDefines.Version;

			this._execute.clicked += this.OnExecute;
			this._remove.clicked += this.OnRemove;

			if (this._nailDesignSelect.FirstDesignName != null) {
				this.OnSelectNail(this._nailDesignSelect.FirstDesignName);
			} else {
				this.UpdatePreview();
			}

			if (Selection.activeGameObject != null) {
				VRCAvatarDescriptor? descriptor = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>();
				
				if (descriptor != null) {
					this._avatarObjectField.value = descriptor;

					AvatarMatching avatarMatching = new(descriptor);
					(Shop shop, Avatar avatar, AvatarVariation variation)? variation = avatarMatching.Match();
					if (variation != null && this._avatarDropDowns != null) {
						this._avatarDropDowns.SetValues(variation.Value.shop, variation.Value.avatar, variation.Value.variation);
					}
				}
			}
		}

		private void OnChangeAvatarSort(ChangeEvent<AvatarSortOrder> evt) {
			this._avatarDropDowns?.Sort(evt.newValue);
		}

		private void OnChangeAvatar(ChangeEvent<Object> evt) {
			if (evt.newValue == null) return;
			if (evt.newValue is not VRCAvatarDescriptor avatar) return;
			AvatarMatching avatarMatching = new(avatar);
			(Shop shop, Avatar avatar, AvatarVariation variation)? variation = avatarMatching.Match();
			if (variation == null) return;
			this._avatarDropDowns!.SetValues(variation.Value.shop, variation.Value.avatar, variation.Value.variation);
		}

		private void OnDestroy() {
			INailProcessor.ClearPreviewMaterialCash();
		}

private void OnExecute() {
			VRCAvatarDescriptor? avatar = this._avatarObjectField!.value as VRCAvatarDescriptor;
			if (avatar == null) {
				Debug.LogError("Not found target Avatar.");
				EditorUtility.DisplayDialog(S("dialog.error"), S("dialog.error.select_target_avatar"), "OK");
				return;
			}

			AvatarVariation? avatarVariationData = this._avatarDropDowns!.GetSelectedAvatarVariation();
			if (avatarVariationData == null) {
				Debug.LogError("Not found Avatar.");
				return;
			}

			GameObject? prefab = this._avatarDropDowns!.GetSelectedPrefab();
			if (prefab == null) {
				Debug.LogError("Not found target Nail Prefabs.");
				return;
			}

			string? nailShapeName = this._nailShapeDropDown!.value;
			if (nailShapeName == null) {
				Debug.LogError("not selected nail shape.");
				return;
			}

			(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();
			NailSetupProcessor processor = new(avatar, avatarVariationData, prefab, designAndVariationNames, nailShapeName) {
				AvatarName = this._avatarDropDowns.GetAvatarName(),
				OverrideMesh = this._nailShapeDropDown!.GetSelectedShapeMeshes(),
				UseFootNail = this._useFootNail!.value,
				RemoveCurrentNail = this._removeCurrentNail!.value,
				GenerateMaterial = true,
				Backup = this._backup!.value,
				ForModularAvatar = this._forModularAvatar!.value
			};
			processor.Process();

			Dictionary<string, DateTime> lastUsedTimes = GlobalSetting.DesignLastUsedTimes;
			Dictionary<string, int> designUsedCounts = GlobalSetting.DesignUseCount;
			Dictionary<string, int> avatarUseCount = GlobalSetting.AvatarUseCount;
			
			HashSet<string> uniqueDesignNames = new();
			foreach ((INailProcessor nailProcessor, string _, string _) in designAndVariationNames) {
				if (!string.IsNullOrEmpty(nailProcessor.DesignName)) {
					uniqueDesignNames.Add(nailProcessor.DesignName);
				}
			}

			foreach (string dName in uniqueDesignNames) {
				lastUsedTimes[dName] = DateTime.Now;
				designUsedCounts[dName] = designUsedCounts.GetValueOrDefault(dName, 0) + 1;
			}

			string avatarKey = this._avatarDropDowns!.GetAvatarKey();
			avatarUseCount[avatarKey] = avatarUseCount.GetValueOrDefault(avatarKey, 0) + 1;

			GlobalSetting.DesignLastUsedTimes = lastUsedTimes;
			GlobalSetting.DesignUseCount = designUsedCounts;
			GlobalSetting.AvatarUseCount = avatarUseCount;
			this._nailDesignSelect!.Init();

			EditorUtility.DisplayDialog(S("dialog.finished"), S("dialog.finished.success_attach_nail"), "OK");
		}

		private void OnRemove() {
			VRCAvatarDescriptor? avatar = this._avatarObjectField!.value as VRCAvatarDescriptor;
			if (avatar == null) {
				Debug.LogError("Not found target Avatar.");
				EditorUtility.DisplayDialog(S("dialog.error"), S("dialog.error.select_target_avatar"), "OK");
				return;
			}
			
			AvatarVariation? avatarVariationData = this._avatarDropDowns!.GetSelectedAvatarVariation();
			if (avatarVariationData == null) {
				Debug.LogError("Not found Avatar.");
				return;
			}

			NailSetupProcessor.RemoveNail(avatar, avatarVariationData.BoneMappingOverride);
		}

		private void OnSelectNail(string designName) {
			using DBNailDesign dbNailDesign = new();
			NailDesign? design = dbNailDesign.FindNailDesignByDesignName(designName);
			if (design?.DesignName == null) return;
			INailProcessor nailProcessor = INailProcessor.CreateNailDesign(design.DesignName);
			List<string> materialPopupElements = design.MaterialVariation switch {
				null => new List<string> {""},
				_ => design.MaterialVariation
					.Where(pair => nailProcessor.IsInstalledMaterialVariation(pair.Value.MaterialName))
					.Select(pair => pair.Value.MaterialName)
					.ToList()
			};
			string materialValue = materialPopupElements.Count <= 0 ? "" : materialPopupElements[0];

			List<string> colorPopupElements = design.ColorVariation
				.Where(pair => nailProcessor.IsInstalledColorVariation(materialValue, pair.Value.ColorName))
				.Select(pair => pair.Value.ColorName)
				.ToList();
			string colorValue = colorPopupElements.Count <= 0 ? "" : colorPopupElements[0];
			
			foreach (NailDesignDropDowns nailDesignDropDowns in this._nailDesignDropDowns!) {
				nailDesignDropDowns.SetValue(designName, materialValue, materialPopupElements, colorValue, colorPopupElements);
			}

			this._nailMaterialDropDown!.choices = materialPopupElements;
			this._nailMaterialDropDown!.SetValueWithoutNotify(materialValue);

			this._nailColorDropDown!.choices = colorPopupElements;
			this._nailColorDropDown!.SetValueWithoutNotify(colorValue);
			
			this.UpdateNailShapeFilter(nailProcessor);
			this.UpdatePreview();
		}

		private void OnChangeMaterial(ChangeEvent<Object?> evt) {
			this.UpdateNailShapeFilter();
			this.UpdatePreview();
		}

		private void OnChangeShapeDropDown(ChangeEvent<string> evt) {
			GlobalSetting.LastUseShapeName = evt.newValue;
			this.UpdatePreview();
		}

		private void OnChangeNailMaterialDropDown(ChangeEvent<string?> evt) {
			string? materialValue = evt.newValue;
			if (materialValue == null) return;
			foreach (NailDesignDropDowns nailDesignDropDowns in this._nailDesignDropDowns!) {
				nailDesignDropDowns.SetMaterialValue(materialValue);
			}
			
			this.UpdatePreview();
		}

		private void OnChangeNailColorDropDown(ChangeEvent<string?> evt) {
			string? colorValue = evt.newValue;
			if (colorValue == null) return;
			foreach (NailDesignDropDowns nailDesignDropDowns in this._nailDesignDropDowns!) {
				nailDesignDropDowns.SetColorValue(colorValue);
			}

			this.UpdatePreview();
		}

		private void OnChangeNailDesign(ChangeEvent<string?> evt) {
			if (evt.target is DropdownField { name: "NailDesignDropDowns-DesignDropDown" }) {
				this.UpdateNailShapeFilter();
			}
			this.UpdatePreview();
		}

		private void OnChangeSetPreFinger(ChangeEvent<bool> evt) {
			this._handSelects!.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void OnChangeUseFootNail(ChangeEvent<bool> evt) {
			this._footSelects!.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
			GlobalSetting.UseFootNail = evt.newValue;
		}

		private void UpdateNailShapeFilter(INailProcessor? processor = null) {
			if (this._materialObjectField!.value != null) {
				this._nailShapeDropDown!.SetFilter(_ => true);
				return;
			}
			
			if (processor != null) {
				this._nailShapeDropDown!.SetFilter(processor.IsSupportedNailShape);
				return;
			}
			
			HashSet<string> designNameSet = this._nailDesignDropDowns!.Select(downs => downs.GetSelectedDesignName()).ToHashSet();
			using DBNailDesign dbNailDesign = new();
			List<INailProcessor> processors = designNameSet.Select(INailProcessor.CreateNailDesign).ToList();
			this._nailShapeDropDown!.SetFilter(shapeName => processors.All(nailProcessor => nailProcessor.IsSupportedNailShape(shapeName)));
		}

		private void UpdatePreview() {
			string? nailShapeName = this._nailShapeDropDown!.value;
			if (nailShapeName == null) {
				Debug.LogError("not selected nail shape.");
				return;
			}

			Mesh?[]? overrideMeshes = this._nailShapeDropDown!.GetSelectedShapeMeshes();
			if (overrideMeshes == null) {
				Debug.LogError("overrideMeshes is null.");
				return;
			}

			this._nailPreviewController!.ChangeNailShape(overrideMeshes);
			this._nailPreviewController!.ChangeFootNailMesh(this._nailShapeDropDown!.value);

			(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();

			this._nailPreviewController.ChangeNailMaterial(designAndVariationNames, nailShapeName);
			this._nailPreviewController.ChangeAdditionalObjects(designAndVariationNames, nailShapeName);
		}

		private (INailProcessor, string, string)[] GetNailProcessors() {
			if (this._materialObjectField!.value != null && this._materialObjectField.value is Material directMaterial) {
				INailProcessor directNailProcessor = new DirectMaterialProcessor(directMaterial);
				return this._nailDesignDropDowns!
					.Select(_ => (directNailProcessor, string.Empty, string.Empty))
					.ToArray();
			}
			
			(string designName, string materialName, string colorName)[] designNameAndVariationNames = this._nailDesignDropDowns!
				.Select(dropDowns => dropDowns.GetSelectedDesignAndVariationName())
				.ToArray();

			Dictionary<string, INailProcessor> designDictionary = new();

			return designNameAndVariationNames
				.Select(tuple => {
					if (designDictionary.TryGetValue(tuple.designName, out INailProcessor nailDesign)) {
						return (nailDesign, tuple.materialName, tuple.colorName);
					}

					INailProcessor newProcessor = INailProcessor.CreateNailDesign(tuple.designName);
					designDictionary.Add(tuple.designName, newProcessor);
					return (newProcessor, tuple.materialName, tuple.colorName);
				})
				.ToArray();
		}
		
		private static void OnChangeRemoveCurrentNail(ChangeEvent<bool> evt) {
			GlobalSetting.RemoveCurrentNail = evt.newValue;
		}
		
		private static void OnChangeBackup(ChangeEvent<bool> evt) {
			GlobalSetting.Backup = evt.newValue;
		}
		
		private static void OnChangeForModularAvatar(ChangeEvent<bool> evt) {
			GlobalSetting.UseModularAvatar = evt.newValue;
		}

		private void ShowAvatarSearchWindow() {
			SearchAvatarWindow.ShowWindow(this);
		}

		private void ShowNailSearchWindow() {
			SearchNailDesignWindow.ShowWindow(this);
		}
		
		public void SelectNailFromSearch(string designName) {
			this.OnSelectNail(designName);
		}

		// 着用回数リセット
		private void MigrateUsageStats() {
			const string OLD_DESIGN_KEY = "world.anlabo.mdnailtool.design_use_count";
			const string OLD_AVATAR_KEY = "world.anlabo.mdnailtool.avatar_use_count";

			if (!EditorPrefs.HasKey(OLD_DESIGN_KEY)) return;

			var oldDesignCounts = JsonConvert.DeserializeObject<Dictionary<string, int>>(EditorPrefs.GetString(OLD_DESIGN_KEY));
			if (oldDesignCounts != null) {
				var migrated = new Dictionary<string, int>();
				foreach (var kvp in oldDesignCounts) migrated[kvp.Key] = Mathf.CeilToInt(kvp.Value / 24.0f);
				GlobalSetting.DesignUseCount = migrated;
			}
            
			EditorPrefs.DeleteKey(OLD_DESIGN_KEY);
			EditorPrefs.DeleteKey(OLD_AVATAR_KEY);
		}
	}
}
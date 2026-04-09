using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
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
			window.titleContent = new GUIContent("An-Labo NailTool");
			window.Show();
		}

		#region Constants & Fields

		private const string SCENE_PREVIEW_NAME = "[MDNailTool_Preview]";

		private Toggle? _enableDirectMaterial;
		private ObjectField? _materialObjectField;
		private LocalizedObjectField? _avatarObjectField;
		private AvatarDropDowns? _avatarDropDowns;
		private NailDesignSelect? _nailDesignSelect;
		private NailPreview? _nailPreview;
		private NailShapeDropDown? _nailShapeDropDown;
		private LocalizedDropDown? _nailMaterialDropDown;
		private LocalizedDropDown? _nailColorDropDown;
		private DropdownField? _nailVariantDropDown;
		private Dictionary<string, string>? _variantDisplayNames;

		private NailDesignDropDowns[]? _nailDesignDropDowns;

		private Toggle? _tglHandActive;
		private Toggle? _tglHandDetail;
		private Toggle? _tglFootActive;
		private Toggle? _tglFootDetail;

		private Toggle? _removeCurrentNail;
		private Toggle? _backup;
		private Toggle? _enableScenePreview;
		private Toggle? _forModularAvatar;
		private Toggle? _generateExpressionMenu;
		private Toggle? _splitHandFootExpressionMenu;
		private Toggle? _mergeAnLaboExpressionMenu;
		private Toggle? _armatureScaleCompensation;
		private Toggle? _penetrationCorrection;
		private Toggle? _bakeBlendShapes;
		private Toggle? _syncBlendShapesWithMA;
		private DropdownField? _additionalMaterialSourceDropdown;
		private DropdownField? _additionalObjectSourceDropdown;
		private LocalizedButton? _execute;
		private LocalizedButton? _remove;

		private IVisualElementScheduledItem? _scenePreviewSchedule;
		private const int SCENE_PREVIEW_DEBOUNCE_MS = 150;

		private NailPreviewController? _nailPreviewController;

		private VisualElement? _handSelects;
		private VisualElement? _footSelects;

		private Label? _manualLink;
		private LocalizedLabel? _contactLink;

		// ---- Hand/Foot section headers (for error highlight) ----
		private VisualElement? _handSectionHeader;
		private VisualElement? _footSectionHeader;

		// ---- Error Banner ----
		private VisualElement? _errorBanner;
		private Label? _errorMessage;
		private Label? _errorDetailToggle;
		private VisualElement? _errorDetailArea;
		private Label? _errorDetailText;
		private bool _errorDetailExpanded = false;
		private int _userErrorCount = 0;
		private VisualElement? _contactLinksArea;

		// ---- Warning Banner ----
		private VisualElement? _warningBanner;
		private Label? _warningMessage;
		private Label? _warningDetailToggle;
		private VisualElement? _warningDetailArea;
		private Label? _warningDetailText;
		private bool _warningDetailExpanded = false;

		// ---- Tool Console ----
		private Toggle? _enableToolConsole;
		private VisualElement? _toolConsoleContainer;
		private ScrollView? _toolConsoleScroll;

		#endregion

		public void SetAvatar(Shop shop, Avatar? avatar, AvatarVariation? variation)
		{
			this._avatarDropDowns?.SetValues(shop, avatar, variation);
		}

		public void CreateGUI()
		{
			this.titleContent = new GUIContent("An-Labo NailTool");
			this.PrepareOnCreateGUI();
			this.BuildRootUI();
			this.BindCoreFields();
			this.BindAvatarUI();
			this.BindNailUI();
			this.BindHandFootUI();
			this.BindOptionsUI();
			this.BindLinksUI();
			this.BindErrorBanner();
			this.BindWarningBanner();
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
			var uss = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(MDNailToolGuids.WindowUss, MDNailToolGuids.WindowUssPath);
			if (uss != null)
			{
				this.rootVisualElement.styleSheets.Add(uss);
			}

			var uxml = MDNailToolAssetLoader.LoadByGuid<VisualTreeAsset>(MDNailToolGuids.WindowUxml, MDNailToolGuids.WindowUxmlPath);
			if (uxml != null)
			{
				uxml.CloneTree(this.rootVisualElement);
			}
		}
		private void BindCoreFields()
		{
			this._enableDirectMaterial = this.rootVisualElement.Q<Toggle>("enable-direct-material");

			// ObjectField を C# 側で生成してトグル行に追加
			var directMaterialRow = this.rootVisualElement.Q<VisualElement>("direct-material-row");
			this._materialObjectField = new ObjectField {
				name = "material-object",
				label = "",
				objectType = typeof(Material),
				style = {
					flexGrow = 1,
					flexShrink = 1,
				}
			};
			this._materialObjectField.RegisterValueChangedCallback(this.OnChangeMaterial);
			directMaterialRow?.Add(this._materialObjectField);

			if (this._enableDirectMaterial != null)
			{
				this._enableDirectMaterial.SetValueWithoutNotify(false);
				this._materialObjectField.style.display = DisplayStyle.None;
				this._enableDirectMaterial.RegisterValueChangedCallback(this.OnChangeEnableDirectMaterial);
				var lblEnableDirectMat = this.rootVisualElement.Q<LocalizedLabel>("label-enable-direct-material");
				lblEnableDirectMat?.RegisterCallback<ClickEvent>(_ => {
					if (this._enableDirectMaterial != null) this._enableDirectMaterial.value = !this._enableDirectMaterial.value;
				});
			}

			this._avatarObjectField = this.rootVisualElement.Q<LocalizedObjectField>("avatar-object");
			this._avatarObjectField.RegisterValueChangedCallback(this.OnChangeAvatar);
		}
		private void BindAvatarUI()
		{
			this._avatarDropDowns = this.rootVisualElement.Q<AvatarDropDowns>("avatar");
			if (this._avatarDropDowns == null) return;

			// tooltip.avatar_dropdowns はUxmlTraitsが非対応のためC#で設定
			this._avatarDropDowns.tooltip = S("tooltip.avatar_dropdowns");

			this._avatarDropDowns.SearchButtonClicked += this.ShowAvatarSearchWindow;
			this._avatarDropDowns.SortOrderSelected += this.OnChangeAvatarSortOrder;

			this._avatarDropDowns.RegisterCallback<ChangeEvent<string>>(evt =>
			{
				this.CleanupScenePreview();
				this.UpdatePreview();
				this.RequestScenePreviewUpdate();
				
				if (evt.target != this._avatarDropDowns.BlendShapeVariantPopup)
				{
					this.UpdateBlendShapeVariantDropDown();
				}
			});
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
			NailDesignDropDowns.AddArrowKeyNavigation(this._nailShapeDropDown);

			this._nailMaterialDropDown = this.rootVisualElement.Q<LocalizedDropDown>("nail-material");
			this._nailMaterialDropDown.RegisterValueChangedCallback(this.OnChangeNailMaterialDropDown);
			NailDesignDropDowns.AddArrowKeyNavigation(this._nailMaterialDropDown);

			this._nailColorDropDown = this.rootVisualElement.Q<LocalizedDropDown>("nail-color");
			this._nailColorDropDown.RegisterValueChangedCallback(this.OnChangeNailColorDropDown);
			NailDesignDropDowns.AddArrowKeyNavigation(this._nailColorDropDown);

			// バリアント選択ドロップダウンをマテリアルバリエーション欄に配置（初期は非表示）
			// バリアントがあるデザインの場合、マテリアルドロップダウンを隠してこちらを表示する
			this._nailVariantDropDown = new DropdownField { label = "バリアント" };
			this._nailVariantDropDown.AddToClassList("mdn-style-dropdown");
			this._nailVariantDropDown.style.display = DisplayStyle.None;
			this._nailVariantDropDown.RegisterValueChangedCallback(this.OnChangeNailVariantDropDown);
			NailDesignDropDowns.AddArrowKeyNavigation(this._nailVariantDropDown);
			// マテリアルドロップダウンの直後に挿入（同じmdn-style-item内）
			this._nailMaterialDropDown!.parent.Insert(
				this._nailMaterialDropDown.parent.IndexOf(this._nailMaterialDropDown) + 1,
				this._nailVariantDropDown);
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
			// ラベルクリックでトグル
			var lblRemove = this.rootVisualElement.Q<LocalizedLabel>("label-remove-nail");
			lblRemove?.RegisterCallback<ClickEvent>(_ => { if (this._removeCurrentNail != null) this._removeCurrentNail.value = !this._removeCurrentNail.value; });

			this._backup = this.rootVisualElement.Q<Toggle>("backup");
			this._backup.SetValueWithoutNotify(GlobalSetting.Backup);
			this._backup.RegisterValueChangedCallback(OnChangeBackup);
			var lblBackup = this.rootVisualElement.Q<LocalizedLabel>("label-backup");
			lblBackup?.RegisterCallback<ClickEvent>(_ => { if (this._backup != null) this._backup.value = !this._backup.value; });

			// プレビュー（常時ON、ヘッダー非表示）
			this._enableScenePreview = this.rootVisualElement.Q<Toggle>("enable-scene-preview");
			if (this._enableScenePreview != null)
			{
				this._enableScenePreview.SetValueWithoutNotify(true);
				this._enableScenePreview.parent.style.display = DisplayStyle.None;
				this.UpdatePreviewAreaVisibility(true);
			}

			// 着用プレビュー（Sceneプレビュー）トグル（詳細設定内）
			var tglWearingPreview = this.rootVisualElement.Q<Toggle>("enable-wearing-preview");
			if (tglWearingPreview != null)
			{
				tglWearingPreview.SetValueWithoutNotify(GlobalSetting.EnableSceneWearingPreview);
				tglWearingPreview.RegisterValueChangedCallback(this.OnChangeEnableScenePreview);
			}
			var lblWearingPreview = this.rootVisualElement.Q<LocalizedLabel>("label-wearing-preview");
			lblWearingPreview?.RegisterCallback<ClickEvent>(_ => { if (tglWearingPreview != null) tglWearingPreview.value = !tglWearingPreview.value; });

			this._forModularAvatar = this.rootVisualElement.Q<Toggle>("for-modular-avatar");
			if (this._forModularAvatar != null)
			{
				this._forModularAvatar.SetValueWithoutNotify(GlobalSetting.UseModularAvatar);
				this._forModularAvatar.RegisterValueChangedCallback(this.OnChangeForModularAvatar);
				var lblMA = this.rootVisualElement.Q<LocalizedLabel>("label-modular-avatar");
				lblMA?.RegisterCallback<ClickEvent>(_ => {
					if (this._forModularAvatar != null) this._forModularAvatar.value = !this._forModularAvatar.value;
				});
			}

			this._generateExpressionMenu = this.rootVisualElement.Q<Toggle>("generate-expression-menu");
			if (this._generateExpressionMenu != null)
			{
				this._generateExpressionMenu.SetValueWithoutNotify(GlobalSetting.GenerateExpressionMenu);
				this._generateExpressionMenu.RegisterValueChangedCallback(evt => {
					GlobalSetting.GenerateExpressionMenu = evt.newValue;
					this.UpdateExpressionMenuSubOptions(evt.newValue);
				});
				this._generateExpressionMenu.SetEnabled(GlobalSetting.UseModularAvatar);
				var lblGenMenu = this.rootVisualElement.Q<LocalizedLabel>("label-generate-expression-menu");
				lblGenMenu?.RegisterCallback<ClickEvent>(_ => {
					if (this._generateExpressionMenu != null && this._generateExpressionMenu.enabledSelf)
						this._generateExpressionMenu.value = !this._generateExpressionMenu.value;
				});
			}

			this._splitHandFootExpressionMenu = this.rootVisualElement.Q<Toggle>("split-hand-foot-expression-menu");
			if (this._splitHandFootExpressionMenu != null)
			{
				this._splitHandFootExpressionMenu.SetValueWithoutNotify(GlobalSetting.SplitHandFootExpressionMenu);
				this._splitHandFootExpressionMenu.RegisterValueChangedCallback(
					evt => GlobalSetting.SplitHandFootExpressionMenu = evt.newValue);
				this._splitHandFootExpressionMenu.SetEnabled(GlobalSetting.UseModularAvatar && GlobalSetting.GenerateExpressionMenu);
				var lbl = this.rootVisualElement.Q<LocalizedLabel>("label-split-hand-foot");
				lbl?.RegisterCallback<ClickEvent>(_ => {
					if (this._splitHandFootExpressionMenu != null && this._splitHandFootExpressionMenu.enabledSelf)
						this._splitHandFootExpressionMenu.value = !this._splitHandFootExpressionMenu.value;
				});
			}

			this._mergeAnLaboExpressionMenu = this.rootVisualElement.Q<Toggle>("merge-anlabo-expression-menu");
			if (this._mergeAnLaboExpressionMenu != null)
			{
				this._mergeAnLaboExpressionMenu.SetValueWithoutNotify(GlobalSetting.MergeAnLaboExpressionMenu);
				this._mergeAnLaboExpressionMenu.RegisterValueChangedCallback(
					evt => GlobalSetting.MergeAnLaboExpressionMenu = evt.newValue);
				this._mergeAnLaboExpressionMenu.SetEnabled(GlobalSetting.UseModularAvatar && GlobalSetting.GenerateExpressionMenu);
				var lbl = this.rootVisualElement.Q<LocalizedLabel>("label-merge-anlabo");
				lbl?.RegisterCallback<ClickEvent>(_ => {
					if (this._mergeAnLaboExpressionMenu != null && this._mergeAnLaboExpressionMenu.enabledSelf)
						this._mergeAnLaboExpressionMenu.value = !this._mergeAnLaboExpressionMenu.value;
				});
			}

			// Armature補正（常時ON、トグル非表示）
			this._armatureScaleCompensation = this.rootVisualElement.Q<Toggle>("armature-scale-compensation");
			if (this._armatureScaleCompensation != null)
			{
				this._armatureScaleCompensation.SetValueWithoutNotify(true);
				this._armatureScaleCompensation.parent.style.display = DisplayStyle.None;
			}

			this._penetrationCorrection = this.rootVisualElement.Q<Toggle>("enable-penetration-correction");
			if (this._penetrationCorrection != null)
			{
				this._penetrationCorrection.SetValueWithoutNotify(GlobalSetting.EnablePenetrationCorrection);
				this._penetrationCorrection.RegisterValueChangedCallback(evt => {
					GlobalSetting.EnablePenetrationCorrection = evt.newValue;
				});
				var lblPenetration = this.rootVisualElement.Q<LocalizedLabel>("label-penetration-correction");
				lblPenetration?.RegisterCallback<ClickEvent>(_ => {
					if (this._penetrationCorrection != null && this._penetrationCorrection.enabledSelf)
						this._penetrationCorrection.value = !this._penetrationCorrection.value;
				});
			}

			this._bakeBlendShapes = this.rootVisualElement.Q<Toggle>("bake-blendshapes");
			if (this._bakeBlendShapes != null)
			{
				this._bakeBlendShapes.SetValueWithoutNotify(GlobalSetting.BakeBlendShapes);
				this._bakeBlendShapes.RegisterValueChangedCallback(evt => {
					GlobalSetting.BakeBlendShapes = evt.newValue;
					this.UpdateBlendShapeVariantDropDown();
				});
				this._bakeBlendShapes.SetEnabled(GlobalSetting.UseModularAvatar);
				var lblBake = this.rootVisualElement.Q<LocalizedLabel>("label-bake-blendshapes");
				lblBake?.RegisterCallback<ClickEvent>(_ => {
					if (this._bakeBlendShapes != null && this._bakeBlendShapes.enabledSelf)
						this._bakeBlendShapes.value = !this._bakeBlendShapes.value;
				});
			}

			// MA BlendShape Sync（常時ON、トグル非表示）
			this._syncBlendShapesWithMA = this.rootVisualElement.Q<Toggle>("sync-blendshapes-with-ma");
			if (this._syncBlendShapesWithMA != null)
			{
				this._syncBlendShapesWithMA.SetValueWithoutNotify(true);
				this._syncBlendShapesWithMA.parent.style.display = DisplayStyle.None;
			}

			this.UpdateMASubOptionsVisibility(GlobalSetting.UseModularAvatar);

			// 追加マテリアルソース選択ドロップダウン
			this._additionalMaterialSourceDropdown = this.rootVisualElement.Q<DropdownField>("additional-material-source");
			if (this._additionalMaterialSourceDropdown != null)
			{
				this.PopulateAdditionalMaterialSourceDropdown();
				NailDesignDropDowns.AddArrowKeyNavigation(this._additionalMaterialSourceDropdown);
				this._additionalMaterialSourceDropdown.RegisterValueChangedCallback(evt =>
				{
					string? noneLabel = this._additionalMaterialSourceDropdown.choices.FirstOrDefault();
					string? selected = evt.newValue == noneLabel ? null : evt.newValue;
					GlobalSetting.AdditionalMaterialSourceDesign = selected;

					this.SyncPerFingerAdditionalMaterial(evt.newValue);
					this.UpdatePreview();
					this.RequestScenePreviewUpdate();
				});
			}

			// 追加オブジェクトソース選択ドロップダウン
			this._additionalObjectSourceDropdown = this.rootVisualElement.Q<DropdownField>("additional-object-source");
			if (this._additionalObjectSourceDropdown != null)
			{
				this.PopulateAdditionalObjectSourceDropdown();
				NailDesignDropDowns.AddArrowKeyNavigation(this._additionalObjectSourceDropdown);
				this._additionalObjectSourceDropdown.RegisterValueChangedCallback(evt =>
				{
					string? noneLabel = this._additionalObjectSourceDropdown.choices.FirstOrDefault();
					string? selected = evt.newValue == noneLabel ? null : evt.newValue;
					GlobalSetting.AdditionalObjectSourceDesign = selected;

					this.SyncPerFingerAdditionalObject(evt.newValue);
					this.UpdatePreview();
					this.RequestScenePreviewUpdate();
				});
			}

			// トラブルシューティング
			this._enableToolConsole = this.rootVisualElement.Q<Toggle>("enable-tool-console");
			this._toolConsoleContainer = this.rootVisualElement.Q<VisualElement>("tool-console-container");
			this._toolConsoleScroll = this.rootVisualElement.Q<ScrollView>("tool-console-scroll");
			if (this._enableToolConsole != null)
			{
				this._enableToolConsole.SetValueWithoutNotify(GlobalSetting.EnableToolConsole);
				this._enableToolConsole.RegisterValueChangedCallback(evt =>
				{
					GlobalSetting.EnableToolConsole = evt.newValue;
					this.UpdateToolConsoleVisibility(evt.newValue);
				});
				this.UpdateToolConsoleVisibility(GlobalSetting.EnableToolConsole);
			}
			var lblConsole = this.rootVisualElement.Q<Label>("label-tool-console");
			lblConsole?.RegisterCallback<ClickEvent>(_ =>
			{
				if (this._enableToolConsole != null) this._enableToolConsole.value = !this._enableToolConsole.value;
			});

			// ログタイトル設定
			var consoleTitle = this.rootVisualElement.Q<Label>("tool-console-title");
			if (consoleTitle != null) consoleTitle.text = S("window.debug_log_title") ?? "Log";

			// サポート情報コピーボタン
			var copyBtn = this.rootVisualElement.Q<Button>("tool-console-copy");
			if (copyBtn != null) copyBtn.text = S("window.debug_copy") ?? "Copy Support Info";
			copyBtn?.RegisterCallback<ClickEvent>(_ =>
			{
				if (this._toolConsoleScroll == null) return;
				var lines = this._toolConsoleScroll.Children()
					.OfType<Label>()
					.Select(l => l.text);
				string text = string.Join("\n", lines);
				text += this.BuildConsoleDiagnosticInfo();
				EditorGUIUtility.systemCopyBuffer = text;
			});

			// ToolConsole コールバック接続
			ToolConsole.OnLog = this.AppendConsoleLog;
			ToolConsole.Flush();
		}

		private void PopulateAdditionalMaterialSourceDropdown()
		{
			if (this._additionalMaterialSourceDropdown == null) return;

			var choices = new List<string>();
			string noneLabel = S("window.additional_material_source_none") ?? "なし";
			choices.Add(noneLabel);

			// レジストリの名前を表示（GUIDが1つでも有効なもののみ）
			var registry = DBAdditionalAssets.Load();
			if (registry.Materials != null)
			{
				foreach (var kv in registry.Materials)
				{
					if (HasAnyValidGuid(kv.Value))
						choices.Add(kv.Key);
				}
			}

			this._additionalMaterialSourceDropdown.choices = choices;
			this.PopulatePerFingerAdditionalMaterialDropdowns(choices);

			// 保存された選択を復元
			string? saved = GlobalSetting.AdditionalMaterialSourceDesign;
			if (!string.IsNullOrEmpty(saved) && choices.Contains(saved!))
			{
				this._additionalMaterialSourceDropdown.SetValueWithoutNotify(saved!);
				this.SyncPerFingerAdditionalMaterial(saved!);
				return;
			}

			this._additionalMaterialSourceDropdown.SetValueWithoutNotify(noneLabel);
			this.SyncPerFingerAdditionalMaterial(noneLabel);
		}

		private void PopulatePerFingerAdditionalMaterialDropdowns(List<string> choices)
		{
			if (this._nailDesignDropDowns == null) return;
			foreach (var dd in this._nailDesignDropDowns)
			{
				dd.SetAdditionalMaterialChoices(new List<string>(choices));
			}
		}

		private void UpdateAdditionalMaterialSourceDefault()
		{
			if (this._additionalMaterialSourceDropdown == null) return;

			string noneLabel = this._additionalMaterialSourceDropdown.choices.FirstOrDefault() ?? "";

			// 現在選択中のデザインを取得
			string? currentDesignName = this._nailDesignDropDowns?.FirstOrDefault()?.GetSelectedDesignName();
			if (string.IsNullOrEmpty(currentDesignName))
			{
				this._additionalMaterialSourceDropdown.SetValueWithoutNotify(noneLabel);
				GlobalSetting.AdditionalMaterialSourceDesign = null;
				this.SyncPerFingerAdditionalMaterial(noneLabel);
				return;
			}

			// デザインの追加マテリアルGUIDからレジストリ名を逆引き
			using DBNailDesign dbNailDesign = new();
			NailDesign? design = dbNailDesign.FindNailDesignByDesignName(currentDesignName);
			if (design?.AdditionalMaterialGUIDs is { Length: > 0 })
			{
				var registry = DBAdditionalAssets.Load();
				var names = registry.FindMaterialNames(design.AdditionalMaterialGUIDs);
				if (names.Count > 0)
				{
					string registryName = names.First();
					if (this._additionalMaterialSourceDropdown.choices.Contains(registryName))
					{
						this._additionalMaterialSourceDropdown.SetValueWithoutNotify(registryName);
						GlobalSetting.AdditionalMaterialSourceDesign = registryName;
						this.SyncPerFingerAdditionalMaterial(registryName);
						return;
					}
				}
			}

			// デザインに追加マテリアルがない → なしにリセット
			this._additionalMaterialSourceDropdown.SetValueWithoutNotify(noneLabel);
			GlobalSetting.AdditionalMaterialSourceDesign = null;
			this.SyncPerFingerAdditionalMaterial(noneLabel);
		}

		private void SyncPerFingerAdditionalMaterial(string? displayValue)
		{
			if (this._nailDesignDropDowns == null) return;
			foreach (var dd in this._nailDesignDropDowns)
			{
				dd.SetAdditionalMaterialSource(displayValue);
			}
		}

		private IEnumerable<Material>?[]? BuildPerFingerAdditionalMaterials(bool isPreview)
		{
			if (this._nailDesignDropDowns == null) return null;

			string? noneLabel = this._additionalMaterialSourceDropdown?.choices.FirstOrDefault();
			string? globalValue = this._additionalMaterialSourceDropdown?.value;
			string? globalSource = (globalValue == noneLabel) ? null : globalValue;

			string?[] sources = MDNailSelectionBuilder.BuildAdditionalMaterialSources(
				this._nailDesignDropDowns,
				this._tglHandActive?.value ?? true,
				this._tglHandDetail?.value ?? false,
				this._tglFootActive?.value ?? false,
				this._tglFootDetail?.value ?? false,
				globalSource
			);

			var result = new IEnumerable<Material>?[20];
			bool anyNonNull = false;

			// レジストリ名 → GUID → マテリアル を直接解決
			var registry = DBAdditionalAssets.Load();
			for (int i = 0; i < 20; i++)
			{
				string? registryName = sources[i];
				if (string.IsNullOrEmpty(registryName) || registryName == noneLabel) continue;

				var mats = new List<Material>();
				foreach (string resolvedGuid in registry.ResolveMaterialGuids(registryName!))
				{
					string matPath = AssetDatabase.GUIDToAssetPath(resolvedGuid);
					if (string.IsNullOrEmpty(matPath)) continue;
					Material? mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
					if (mat != null) mats.Add(mat);
				}

				if (mats.Count > 0)
				{
					result[i] = mats;
					anyNonNull = true;
				}
			}

			return anyNonNull ? result : null;
		}

		// ---- 追加オブジェクト関連メソッド ----

		/// <summary>GUIDリスト内に1つでも有効なアセットが存在するか</summary>
		private static bool HasAnyValidGuid(IEnumerable<string> guids)
		{
			foreach (string guid in guids)
			{
				if (string.IsNullOrEmpty(guid)) continue;
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (!string.IsNullOrEmpty(path)) return true;
			}
			return false;
		}

		private void PopulateAdditionalObjectSourceDropdown()
		{
			ToolConsole.Log("PopulateAdditionalObjectSourceDropdown 開始");
			if (this._additionalObjectSourceDropdown == null) return;

			var choices = new List<string>();
			string noneLabel = S("window.additional_object_source_none") ?? "なし";
			choices.Add(noneLabel);

			// レジストリの名前を表示（GUIDが1つでも有効なもののみ）
			var registry = DBAdditionalAssets.Load();
			if (registry.Objects != null)
			{
				foreach (var kv in registry.Objects)
				{
					if (HasAnyValidGuid(kv.Value.ResolveGuidsForFinger(0))
					    || (kv.Value.Guids != null && HasAnyValidGuid(kv.Value.Guids)))
						choices.Add(kv.Key);
				}
			}

			this._additionalObjectSourceDropdown.choices = choices;
			ToolConsole.Log($"  choices.Count={choices.Count}");

			// per-finger ドロップダウンにも同じ選択肢を設定
			this.PopulatePerFingerAdditionalObjectDropdowns(choices);

			// 保存された選択を復元
			string? saved = GlobalSetting.AdditionalObjectSourceDesign;
			ToolConsole.Log($"  saved={saved ?? "(null)"}");
			if (!string.IsNullOrEmpty(saved) && choices.Contains(saved!))
			{
				this._additionalObjectSourceDropdown.SetValueWithoutNotify(saved!);
				this.SyncPerFingerAdditionalObject(saved!);
				return;
			}

			this._additionalObjectSourceDropdown.SetValueWithoutNotify(noneLabel);
			this.SyncPerFingerAdditionalObject(noneLabel);
		}

		private void PopulatePerFingerAdditionalObjectDropdowns(List<string> allChoices)
		{
			if (this._nailDesignDropDowns == null) return;

			var registry = DBAdditionalAssets.Load();
			string noneLabel = allChoices.Count > 0 ? allChoices[0] : "";

			foreach (var dd in this._nailDesignDropDowns)
			{
				int fi = dd.GetFingerIndex();
				if (fi < 0 || registry.Objects == null)
				{
					dd.SetAdditionalObjectChoices(new List<string>(allChoices));
					continue;
				}

				// 指インデックスに基づいてフィルタリング + GUID有効性チェック
				var filtered = new List<string> { noneLabel };
				foreach (var kv in registry.Objects)
				{
					if (kv.Value.IsAllowedForFinger(fi) && HasAnyValidGuid(kv.Value.ResolveGuidsForFinger(fi)))
						filtered.Add(kv.Key);
				}
				dd.SetAdditionalObjectChoices(filtered);
			}
		}

		private void UpdateAdditionalObjectSourceDefault()
		{
			if (this._additionalObjectSourceDropdown == null) return;

			string noneLabel = this._additionalObjectSourceDropdown.choices.FirstOrDefault() ?? "";

			// 現在選択中のデザインを取得
			string? currentDesignName = this._nailDesignDropDowns?.FirstOrDefault()?.GetSelectedDesignName();
			if (string.IsNullOrEmpty(currentDesignName))
			{
				this._additionalObjectSourceDropdown.SetValueWithoutNotify(noneLabel);
				GlobalSetting.AdditionalObjectSourceDesign = null;
				this.SyncPerFingerAdditionalObject(noneLabel);
				return;
			}

			// デザインの追加オブジェクトGUIDからレジストリ名を逆引き
			using DBNailDesign dbNailDesign = new();
			NailDesign? design = dbNailDesign.FindNailDesignByDesignName(currentDesignName);
			if (design?.AdditionalObjectGUIDs is { Count: > 0 })
			{
				var registry = DBAdditionalAssets.Load();
				var allGuids = design.AdditionalObjectGUIDs.Values.SelectMany(g => g);
				var names = registry.FindObjectNames(allGuids);
				if (names.Count > 0)
				{
					string registryName = names.First();
					if (this._additionalObjectSourceDropdown.choices.Contains(registryName))
					{
						this._additionalObjectSourceDropdown.SetValueWithoutNotify(registryName);
						GlobalSetting.AdditionalObjectSourceDesign = registryName;
						this.SyncPerFingerAdditionalObject(registryName);
						return;
					}
				}
			}

			// デザインに追加オブジェクトがない → なしにリセット
			this._additionalObjectSourceDropdown.SetValueWithoutNotify(noneLabel);
			GlobalSetting.AdditionalObjectSourceDesign = null;
			this.SyncPerFingerAdditionalObject(noneLabel);
		}

		private void SyncPerFingerAdditionalObject(string? displayValue)
		{
			ToolConsole.Log($"SyncPerFingerAdditionalObject: displayValue={displayValue ?? "(null)"}");
			if (this._nailDesignDropDowns == null) return;

			var registry = DBAdditionalAssets.Load();
			string noneLabel = this._additionalObjectSourceDropdown?.choices.FirstOrDefault() ?? "";

			foreach (var dd in this._nailDesignDropDowns)
			{
				if (string.IsNullOrEmpty(displayValue) || displayValue == noneLabel)
				{
					dd.SetAdditionalObjectSource(displayValue);
					continue;
				}

				int fi = dd.GetFingerIndex();
				if (fi >= 0 && registry.Objects != null &&
				    registry.Objects.TryGetValue(displayValue!, out var entry) &&
				    !entry.IsAllowedForFinger(fi))
				{
					dd.SetAdditionalObjectSource(noneLabel);
				}
				else
				{
					dd.SetAdditionalObjectSource(displayValue);
				}
			}
		}

		private IEnumerable<Transform>?[]? BuildPerFingerAdditionalObjects(bool isPreview)
		{
			ToolConsole.Log($"BuildPerFingerAdditionalObjects: isPreview={isPreview}");
			if (this._nailDesignDropDowns == null)
			{
				ToolConsole.Log("  _nailDesignDropDowns == null → return null");
				return null;
			}

			string? noneLabel = this._additionalObjectSourceDropdown?.choices.FirstOrDefault();
			string? globalValue = this._additionalObjectSourceDropdown?.value;
			string? globalSource = (globalValue == noneLabel) ? null : globalValue;

			ToolConsole.Log($"  globalSource={globalSource ?? "(null)"}, dropdownValue={globalValue ?? "(null)"}");

			// 指ごとのソースを決定（MDNailSelectionBuilder経由）
			string?[] sources = MDNailSelectionBuilder.BuildAdditionalObjectSources(
				this._nailDesignDropDowns,
				this._tglHandActive?.value ?? true,
				this._tglHandDetail?.value ?? false,
				this._tglFootActive?.value ?? false,
				this._tglFootDetail?.value ?? false,
				globalSource
			);

			var result = new IEnumerable<Transform>?[20];
			bool anyNonNull = false;

			// レジストリ名 → GUID → オブジェクト を直接解決（手0-9 + 足10-19）
			var registry = DBAdditionalAssets.Load();
			for (int i = 0; i < 20; i++)
			{
				string? registryName = sources[i];
				if (string.IsNullOrEmpty(registryName) || registryName == noneLabel)
				{
					ToolConsole.Log($"  finger[{i}]: registryName is empty/none → skip");
					continue;
				}

				var transforms = new List<Transform>();
				foreach (string resolvedGuid in registry.ResolveObjectGuids(registryName!, i))
				{
					string objectPath = AssetDatabase.GUIDToAssetPath(resolvedGuid);
					if (string.IsNullOrEmpty(objectPath))
					{
						ToolConsole.Log($"  finger[{i}]: GUID not found: {resolvedGuid} (registryName={registryName})");
						Debug.LogWarning($"[MDNailTool] AdditionalObject GUID not found: {resolvedGuid} (registryName={registryName})");
						continue;
					}
					GameObject? obj = AssetDatabase.LoadAssetAtPath<GameObject>(objectPath);
					if (obj == null)
					{
						ToolConsole.Log($"  finger[{i}]: could not load: {objectPath}");
						Debug.LogWarning($"[MDNailTool] AdditionalObject could not load: {objectPath} (registryName={registryName})");
						continue;
					}
					ToolConsole.Log($"  finger[{i}]: instantiated {obj.name} from {objectPath}");
					transforms.Add(Object.Instantiate(obj, Vector3.zero, Quaternion.identity).transform);
				}

				if (transforms.Count > 0)
				{
					result[i] = transforms;
					anyNonNull = true;
				}
			}

			ToolConsole.Log($"  → result: anyNonNull={anyNonNull}");
			return anyNonNull ? result : null;
		}

		private void UpdateExpressionMenuSubOptions(bool exprMenuEnabled)
		{
			this._splitHandFootExpressionMenu?.SetEnabled(exprMenuEnabled);
			this._mergeAnLaboExpressionMenu?.SetEnabled(exprMenuEnabled);
		}

		private void UpdateBlendShapeVariantDropDown()
		{
			if (this._avatarDropDowns == null) return;
			var popup = this._avatarDropDowns.BlendShapeVariantPopup;
			if (popup == null) return;

			var choices = new List<string> { S("window.none") ?? "None" };
			popup.choices = choices;
			popup.index = 0;

			var avatarVariationData = this._avatarDropDowns.GetSelectedAvatarVariation();
			if (avatarVariationData == null)
			{
				this.UpdateBlendShapeVariantVisibility();
				return;
			}

			AvatarBlendShapeVariant[]? variants = avatarVariationData.BlendShapeVariants;
			if (variants == null)
			{
				using DBShop dbShop = new();
				string avatarName = this._avatarDropDowns.GetAvatarName();
				foreach (Shop s in dbShop.collection)
				{
					Avatar? av = s.FindAvatarByName(avatarName);
					if (av?.BlendShapeVariants != null)
					{
						variants = av.BlendShapeVariants;
						break;
					}
				}
			}

			if (variants != null && variants.Length > 0)
			{
				choices.AddRange(variants.Select(v => v.Name));
				popup.choices = choices;
			}

			this.UpdateBlendShapeVariantVisibility();
		}

		private void UpdateBlendShapeVariantVisibility()
		{
			if (this._avatarDropDowns == null) return;
			var popup = this._avatarDropDowns.BlendShapeVariantPopup;
			if (popup == null) return;

			bool maEnabled = GlobalSetting.UseModularAvatar;
			bool bakeEnabled = maEnabled && GlobalSetting.BakeBlendShapes;

			bool hasVariants = popup.choices.Count > 1;

			popup.style.display = DisplayStyle.Flex;

			if (bakeEnabled)
			{
				popup.SetEnabled(false);
				popup.choices = new List<string> { S("window.blendshape_variant_bake_active") ?? "BlendShape generation is enabled" };
				popup.index = 0;
			}
			else if (!hasVariants)
			{
				popup.SetEnabled(false);
				popup.choices = new List<string> { S("window.blendshape_variant_none") ?? "No BlendShape" };
				popup.index = 0;
			}
			else
			{
				popup.SetEnabled(true);
			}
		}

		private void UpdatePreviewAreaVisibility(bool visible)
		{
			var area = this.rootVisualElement.Q<VisualElement>("nail-preview-area");
			if (area != null)
				area.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void UpdateToolConsoleVisibility(bool visible)
		{
			if (this._toolConsoleContainer != null)
				this._toolConsoleContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void AppendConsoleLog(string message)
		{
			if (this._toolConsoleScroll == null) return;
			var label = new Label(message);
			label.AddToClassList("mdn-tool-console-entry");
			this._toolConsoleScroll.Add(label);

			// 自動スクロール
			this._toolConsoleScroll.schedule.Execute(() =>
			{
				this._toolConsoleScroll.scrollOffset = new Vector2(0, float.MaxValue);
			});
		}

		private string BuildConsoleDiagnosticInfo()
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendLine();
			sb.AppendLine("--- 診断情報 ---");

			sb.AppendLine($"OS: {SystemInfo.operatingSystem}");
			sb.AppendLine($"Unity: {Application.unityVersion}");

			try { sb.AppendLine($"NailTool Version: {MDNailToolDefines.Version}"); }
			catch { sb.AppendLine("NailTool Version: (取得失敗)"); }

			try
			{
				string packageJsonPath = "Packages/nadena.dev.modular-avatar/package.json";
				TextAsset? packageJson = AssetDatabase.LoadAssetAtPath<TextAsset>(packageJsonPath);
				sb.AppendLine($"ModularAvatar: {packageJson?.text switch { string t => Newtonsoft.Json.Linq.JObject.Parse(t)["version"]?.ToString() ?? "unknown", _ => "not installed" }}");
			}
			catch { sb.AppendLine("ModularAvatar: (取得失敗)"); }

			var avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			sb.AppendLine($"Avatar: {avatar?.gameObject?.name ?? "(null)"}");
			sb.AppendLine($"Avatar Root Scale: {avatar?.transform?.localScale.ToString() ?? "(null)"}");
			sb.AppendLine($"AvatarName: {this._avatarDropDowns?.GetAvatarName() ?? "(未設定)"}");
			sb.AppendLine($"Variation: {this._avatarDropDowns?.GetSelectedAvatarVariation()?.VariationName ?? "(null)"}");
			sb.AppendLine($"NailShape: {this._nailShapeDropDown?.value ?? "(null)"}");
			sb.AppendLine($"NailPrefab: {this._avatarDropDowns?.GetSelectedPrefab()?.name ?? "(null)"}");
			sb.AppendLine($"ForModularAvatar: {this._forModularAvatar?.value}");
			sb.AppendLine($"BakeBlendShapes: {this._bakeBlendShapes?.value}");
			sb.AppendLine($"SyncBlendShapesWithMA: {this._syncBlendShapesWithMA?.value}");
			sb.AppendLine($"ArmatureScaleCompensation: {this._armatureScaleCompensation?.value}");
			sb.AppendLine($"UseFootNail: {this._tglFootActive?.value}");
			sb.AppendLine($"HandActive: {this._tglHandActive?.value}");
			sb.AppendLine($"HandDetail: {this._tglHandDetail?.value}");
			sb.AppendLine($"FootDetail: {this._tglFootDetail?.value}");
			sb.AppendLine($"AdditionalObjectSource: {this._additionalObjectSourceDropdown?.value ?? "(null)"}");
			sb.AppendLine($"AdditionalMaterialSource: {this._additionalMaterialSourceDropdown?.value ?? "(null)"}");

			// Body BlendShape状態（値が0でないもののみ）
			if (avatar != null)
			{
				try
				{
					SkinnedMeshRenderer? visemeSmr = avatar.VisemeSkinnedMesh;
					var bodySmr = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
						.Where(smr => smr != visemeSmr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
						.OrderByDescending(smr => smr.sharedMesh!.blendShapeCount)
						.FirstOrDefault();
					if (bodySmr != null && bodySmr.sharedMesh != null)
					{
						sb.AppendLine($"--- Body BlendShapes ({bodySmr.gameObject.name}) ---");
						Mesh mesh = bodySmr.sharedMesh;
						bool hasNonZero = false;
						for (int i = 0; i < mesh.blendShapeCount; i++)
						{
							float weight = bodySmr.GetBlendShapeWeight(i);
							if (weight != 0f)
							{
								sb.AppendLine($"  {mesh.GetBlendShapeName(i)}: {weight:F1}");
								hasNonZero = true;
							}
						}
						if (!hasNonZero) sb.AppendLine("  (all zero)");
					}
				}
				catch { /* BlendShape取得失敗時は無視 */ }
			}

			// NDMF ビルド診断（直近のPlayモード/ビルド時の結果）
#if MD_NAIL_FOR_MA
			if (!string.IsNullOrEmpty(MAPluginDefinition.LastBuildDiagnostic))
			{
				sb.AppendLine("--- NDMF Build Diagnostic ---");
				sb.Append(MAPluginDefinition.LastBuildDiagnostic);
			}
			else
			{
				sb.AppendLine("--- NDMF Build Diagnostic ---");
				sb.AppendLine("(no build data — run Play mode first)");
			}
#endif

			return sb.ToString();
		}

		private void BindLinksUI()
		{
			// Changelog バナーを動的追加
			var bannerContainer = this.rootVisualElement.Q<VisualElement>("changelog-banner-container");
			if (bannerContainer != null) {
				var banner = new ChangelogBanner();
				bannerContainer.Add(banner);
			}

			this._manualLink = this.rootVisualElement.Q<Label>("link-manual");
			if (this._manualLink != null) {
				this._manualLink.text = $"[{S("link.manual.label") ?? "Manual"}]";
				this._manualLink.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.manual")));
			}

			// ヘッダーのカタログリンク
			var catalogLink = this.rootVisualElement.Q<Label>("link-catalog");
			if (catalogLink != null) {
				catalogLink.text = $"[{S("link.catalog.label") ?? "Catalog"}]";
				catalogLink.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.catalog")));
			}

			// ヘッダーのFAQリンク
			var headerContact = this.rootVisualElement.Q<Label>("link-contact-header");
			headerContact?.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.contact")));

			// 着用統計リンク
			var usageStatsLink = this.rootVisualElement.Q<Label>("link-usage-stats");
			if (usageStatsLink != null)
			{
				usageStatsLink.text = $"[{S("usage_stats.link_label") ?? "Usage Stats"}]";
				usageStatsLink.RegisterCallback<ClickEvent>(_ => UsageStatsWindow.ShowWindow());
			}

			// フッターのコンタクトリンク
			this._contactLink = this.rootVisualElement.Q<LocalizedLabel>("link-contact");
			this._contactLink?.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.contact")));

			// ヘッダーのバージョン表記
		var versionStr = MDNailToolDefines.Version;
		var headerVersion = this.rootVisualElement.Q<Label>("version");
		if (headerVersion != null)
			headerVersion.text = "v" + versionStr;

		// フッターのバージョン表記
		var footerVersion = this.rootVisualElement.Q<Label>("version-footer");
		if (footerVersion != null)
			footerVersion.text = versionStr;

			// おすすめ設定ボタン
			AddRecommendButton("mdn-section-header", 3, ApplyRecommendMA);
			AddRecommendButtonToFoldout("mdn-advanced-foldout", ApplyRecommendAdvanced);
		}

		private Button CreateRecommendButton(System.Action onClick)
		{
			string label = S("window.recommend") ?? "Recommended";
			var btn = new Button(onClick) { text = label };
			btn.style.height = 20;
			btn.style.fontSize = 10;
			btn.style.paddingLeft = 8;
			btn.style.paddingRight = 8;
			btn.style.marginLeft = new StyleLength(StyleKeyword.Auto);
			return btn;
		}

		private void AddRecommendButton(string headerClass, int headerIndex, System.Action onClick)
		{
			var headers = this.rootVisualElement.Query(className: headerClass).ToList();
			if (headerIndex < headers.Count)
			{
				var header = headers[headerIndex];
				header.style.flexDirection = FlexDirection.Row;
				header.style.justifyContent = Justify.SpaceBetween;
				header.style.alignItems = Align.Center;
				header.Add(CreateRecommendButton(onClick));
			}
		}

		private void AddRecommendButtonToFoldout(string foldoutClass, System.Action onClick)
		{
			var foldout = this.rootVisualElement.Q(className: foldoutClass);
			if (foldout == null) return;

			var toggle = foldout.Q<Toggle>(className: "unity-foldout__toggle");
			if (toggle == null) return;

			toggle.style.flexDirection = FlexDirection.Row;
			toggle.style.justifyContent = Justify.SpaceBetween;
			toggle.style.alignItems = Align.Center;
			toggle.Add(CreateRecommendButton(onClick));
		}

		private void ApplyRecommendMA()
		{
			if (this._forModularAvatar != null) this._forModularAvatar.value = true;
			if (this._generateExpressionMenu != null) this._generateExpressionMenu.value = true;
			if (this._splitHandFootExpressionMenu != null) this._splitHandFootExpressionMenu.value = true;
			if (this._mergeAnLaboExpressionMenu != null) this._mergeAnLaboExpressionMenu.value = true;
			if (this._bakeBlendShapes != null) this._bakeBlendShapes.value = true;
			if (this._syncBlendShapesWithMA != null) this._syncBlendShapesWithMA.value = true;
		}

		private void ApplyRecommendAdvanced()
		{
			// ON
			if (this._removeCurrentNail != null) this._removeCurrentNail.value = true;
			if (this._backup != null) this._backup.value = true;
			if (this._armatureScaleCompensation != null) this._armatureScaleCompensation.value = true;
			var tglWearingPreview = this.rootVisualElement.Q<Toggle>("enable-wearing-preview");
			if (tglWearingPreview != null) tglWearingPreview.value = true;

			// OFF
			if (this._enableDirectMaterial != null) this._enableDirectMaterial.value = false;
			if (this._penetrationCorrection != null) this._penetrationCorrection.value = false;
			if (this._enableToolConsole != null) this._enableToolConsole.value = false;
		}

		private void BindErrorBanner()
		{
			this._errorBanner = this.rootVisualElement.Q<VisualElement>("error-banner");
			this._errorMessage = this.rootVisualElement.Q<Label>("error-message");
			this._errorDetailToggle = this.rootVisualElement.Q<Label>("error-detail-toggle");
			this._errorDetailArea = this.rootVisualElement.Q<VisualElement>("error-detail-area");
			this._errorDetailText = this.rootVisualElement.Q<Label>("error-detail-text");

			this.rootVisualElement.Q<Button>("error-close")
				?.RegisterCallback<ClickEvent>(_ => {
					this.HideErrorBanner();
					this.ClearHandFootError();
					this.ClearAvatarFieldError();
				});

			this._errorDetailToggle?.RegisterCallback<ClickEvent>(_ => this.ToggleErrorDetail());

			var copyBtn = this.rootVisualElement.Q<Button>("error-copy");
			if (copyBtn != null)
			{
				copyBtn.text = S("error.copy") ?? "Copy to Clipboard";
				copyBtn.RegisterCallback<ClickEvent>(_ =>
					GUIUtility.systemCopyBuffer = this._errorDetailText?.text ?? "");
			}

			var contactBtn = this.rootVisualElement.Q<Button>("error-contact");
			if (contactBtn != null)
			{
				contactBtn.text = S("window.contact") ?? "Contact";
				contactBtn.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.contact")));
			}
		}

		private void ShowErrorBanner(string? userMessage, Exception? ex = null)
		{
			if (this._errorBanner == null) return;
			this._errorBanner.style.display = DisplayStyle.Flex;
			if (this._errorMessage != null) this._errorMessage.text = userMessage ?? "";
			if (this._errorDetailText != null) this._errorDetailText.text = ex?.ToString() ?? "";
			this._errorDetailExpanded = false;
			if (this._errorDetailArea != null) this._errorDetailArea.style.display = DisplayStyle.None;
			if (this._errorDetailToggle != null)
				this._errorDetailToggle.text = S("error.show_detail") ?? "▶ Show Details";
			// Only show the detail toggle when there's exception detail to show
			if (this._errorDetailToggle != null)
				this._errorDetailToggle.style.display = ex != null ? DisplayStyle.Flex : DisplayStyle.None;
			// ScrollView内でエラーバナーが見えるようにスクロール
			var scrollView = this.rootVisualElement.Q<ScrollView>("Root");
			if (scrollView != null && this._errorBanner != null)
				this._errorBanner.schedule.Execute(() => scrollView.ScrollTo(this._errorBanner));
		}

		private void HideErrorBanner()
		{
			if (this._errorBanner != null) this._errorBanner.style.display = DisplayStyle.None;
			if (this._contactLinksArea != null) this._contactLinksArea.style.display = DisplayStyle.None;
		}

		private void ShowContactLinks(string errorText)
		{
			if (this._errorBanner == null) return;

			// 既存のcontactLinksAreaがあれば削除
			if (this._contactLinksArea != null) {
				this._contactLinksArea.RemoveFromHierarchy();
			}

			this._contactLinksArea = new VisualElement();
			this._contactLinksArea.style.marginTop = 8;

			// 問い合わせ案内テキスト
			var contactLabel = new Label(S("error.execute.contact_prompt"));
			contactLabel.style.marginBottom = 4;
			contactLabel.style.whiteSpace = WhiteSpace.Normal;
			this._contactLinksArea.Add(contactLabel);

			// ボタン行
			var buttonRow = new VisualElement();
			buttonRow.style.flexDirection = FlexDirection.Row;
			buttonRow.style.flexWrap = Wrap.Wrap;

			// エラーコピーボタン
			var copyButton = new Button(() => {
				GUIUtility.systemCopyBuffer = errorText;
			});
			copyButton.text = S("error.execute.copy_error");
			copyButton.style.marginRight = 4;
			copyButton.style.marginBottom = 4;
			copyButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
			copyButton.style.color = Color.white;
			buttonRow.Add(copyButton);

			// Discord (紫)
			var discordButton = new Button(() => {
				Application.OpenURL("https://discord.gg/anlabo");
			});
			discordButton.text = "Discord";
			discordButton.style.marginRight = 4;
			discordButton.style.marginBottom = 4;
			discordButton.style.backgroundColor = new Color(0.34f, 0.40f, 0.95f);
			discordButton.style.color = Color.white;
			buttonRow.Add(discordButton);

			// BOOTH (オレンジ)
			var boothButton = new Button(() => {
				Application.OpenURL("https://accounts.booth.pm/conversations/5331544/messages");
			});
			boothButton.text = "BOOTH";
			boothButton.style.marginRight = 4;
			boothButton.style.marginBottom = 4;
			boothButton.style.backgroundColor = new Color(0.82f, 0.17f, 0.20f);
			boothButton.style.color = Color.white;
			buttonRow.Add(boothButton);

			// X (黒)
			var xButton = new Button(() => {
				Application.OpenURL("https://x.com/an_labo_virtual");
			});
			xButton.text = "X";
			xButton.style.marginBottom = 4;
			xButton.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
			xButton.style.color = Color.white;
			buttonRow.Add(xButton);

			this._contactLinksArea.Add(buttonRow);
			this._errorBanner.Add(this._contactLinksArea);
		}

		private void ToggleErrorDetail()
		{
			this._errorDetailExpanded = !this._errorDetailExpanded;
			if (this._errorDetailArea != null)
				this._errorDetailArea.style.display = this._errorDetailExpanded ? DisplayStyle.Flex : DisplayStyle.None;
			if (this._errorDetailToggle != null)
				this._errorDetailToggle.text = this._errorDetailExpanded
					? (S("error.hide_detail") ?? "▼ Hide Details")
					: (S("error.show_detail") ?? "▶ Show Details");
		}

		private void BindWarningBanner()
		{
			this._warningBanner = this.rootVisualElement.Q<VisualElement>("warning-banner");
			this._warningMessage = this.rootVisualElement.Q<Label>("warning-message");
			this._warningDetailToggle = this.rootVisualElement.Q<Label>("warning-detail-toggle");
			this._warningDetailArea = this.rootVisualElement.Q<VisualElement>("warning-detail-area");
			this._warningDetailText = this.rootVisualElement.Q<Label>("warning-detail-text");
			var closeBtn = this.rootVisualElement.Q<Button>("warning-close");
			if (closeBtn != null) closeBtn.clicked += this.HideWarningBanner;
			if (this._warningDetailToggle != null)
				this._warningDetailToggle.RegisterCallback<ClickEvent>(_ => this.ToggleWarningDetail());
			var copyBtn = this.rootVisualElement.Q<Button>("warning-copy");
			if (copyBtn != null)
			{
				copyBtn.text = S("error.copy") ?? "Copy to Clipboard";
				copyBtn.RegisterCallback<ClickEvent>(_ =>
					GUIUtility.systemCopyBuffer = this._warningDetailText?.text ?? "");
			}
		}

		private void ShowWarningBanner(string summary, IReadOnlyList<string> details)
		{
			if (this._warningBanner == null) return;
			this._warningBanner.style.display = DisplayStyle.Flex;
			if (this._warningMessage != null) this._warningMessage.text = summary;
			this._warningDetailExpanded = false;
			if (this._warningDetailArea != null) this._warningDetailArea.style.display = DisplayStyle.None;
			if (this._warningDetailText != null) this._warningDetailText.text = string.Join("\n", details);
			if (this._warningDetailToggle != null)
			{
				this._warningDetailToggle.style.display = details.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
				this._warningDetailToggle.text = S("error.show_detail") ?? "▶ Show Details";
			}
			var scrollView = this.rootVisualElement.Q<ScrollView>("Root");
			if (scrollView != null && this._warningBanner != null)
				this._warningBanner.schedule.Execute(() => scrollView.ScrollTo(this._warningBanner));
		}

		private void HideWarningBanner()
		{
			if (this._warningBanner != null) this._warningBanner.style.display = DisplayStyle.None;
		}

		private void ToggleWarningDetail()
		{
			this._warningDetailExpanded = !this._warningDetailExpanded;
			if (this._warningDetailArea != null)
				this._warningDetailArea.style.display = this._warningDetailExpanded ? DisplayStyle.Flex : DisplayStyle.None;
			if (this._warningDetailToggle != null)
				this._warningDetailToggle.text = this._warningDetailExpanded
					? (S("error.hide_detail") ?? "▼ Hide Details")
					: (S("error.show_detail") ?? "▶ Show Details");
		}

		private void ShowAvatarFieldError()
		{
			this._avatarObjectField?.AddToClassList("mdn-field-error");
		}

		private void ClearAvatarFieldError()
		{
			this._avatarObjectField?.RemoveFromClassList("mdn-field-error");
		}

		private void ShowHandFootError()
		{
			this._handSectionHeader?.AddToClassList("mdn-field-error");
			this._footSectionHeader?.AddToClassList("mdn-field-error");
		}

		private void ClearHandFootError()
		{
			this._handSectionHeader?.RemoveFromClassList("mdn-field-error");
			this._footSectionHeader?.RemoveFromClassList("mdn-field-error");
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
				// インストール済みネイルデザインがない場合、マテリアル直接指定を自動ON
				if (this._enableDirectMaterial != null)
				{
					this._enableDirectMaterial.value = true;
				}

				this.UpdatePreview();
				this.RequestScenePreviewUpdate();
			}

			// Selection.activeGameObject からアバターを検出
			VRCAvatarDescriptor? descriptor = null;
			if (Selection.activeGameObject != null)
			{
				descriptor = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>();
			}

			// 見つからなかった場合、Hierarchy全体をスキャンして1体だけなら自動設定
			if (descriptor == null)
			{
				VRCAvatarDescriptor[] allDescriptors = Object.FindObjectsByType<VRCAvatarDescriptor>(
					FindObjectsInactive.Exclude,
					FindObjectsSortMode.None);
				if (allDescriptors.Length == 1)
				{
					descriptor = allDescriptors[0];
				}
			}

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

			this.UpdateBlendShapeVariantDropDown();
		}


		private void SetupFootVisualElements()
		{
			var footSelects = this.rootVisualElement.Q<VisualElement>("foot-selects");
			this._footSelects = footSelects;

			if (footSelects == null) return;

			footSelects.Clear();

			string[] toeTextIds = { "window.thumb", "window.index_finger", "window.middle_finger", "window.ring_finger", "window.little_finger" };

			// ---- 左足ヘッダー行（ハンドネイルと同じ構造） ----
			var leftHeader = new VisualElement();
			leftHeader.AddToClassList("mdn-finger-header");

			var leftFootColLabel = new LocalizedLabel { TextId = "window.left_foot" };
			leftFootColLabel.AddToClassList("mdn-finger-name-col");
			leftFootColLabel.AddToClassList("mdn-col-header");
			leftHeader.Add(leftFootColLabel);

			var leftDesignHeader = new LocalizedLabel { TextId = "window.nail_design" };
			leftDesignHeader.AddToClassList("mdn-finger-design-col");
			leftDesignHeader.AddToClassList("mdn-col-header");
			leftHeader.Add(leftDesignHeader);

			var leftMatHeader = new LocalizedLabel { TextId = "window.nail_material" };
			leftMatHeader.AddToClassList("mdn-finger-mat-col");
			leftMatHeader.AddToClassList("mdn-col-header");
			leftHeader.Add(leftMatHeader);

			var leftColHeader = new LocalizedLabel { TextId = "window.nail_color" };
			leftColHeader.AddToClassList("mdn-finger-col-col");
			leftColHeader.AddToClassList("mdn-col-header");
			leftHeader.Add(leftColHeader);

			var leftAddMatHeader = new LocalizedLabel { TextId = "window.additional_material" };
			leftAddMatHeader.AddToClassList("mdn-finger-addmat-col");
			leftAddMatHeader.AddToClassList("mdn-col-header");
			leftHeader.Add(leftAddMatHeader);

			var leftAddObjHeader = new LocalizedLabel { TextId = "window.additional_object" };
			leftAddObjHeader.AddToClassList("mdn-finger-addobj-col");
			leftAddObjHeader.AddToClassList("mdn-col-header");
			leftHeader.Add(leftAddObjHeader);

			footSelects.Add(leftHeader);

			// ---- 左足 5本 ----
			string[] leftToes = { "left-foot-thumb", "left-foot-index", "left-foot-middle", "left-foot-ring", "left-foot-little" };
			for (int i = 0; i < 5; i++)
			{
				var dd = new NailDesignDropDowns { name = leftToes[i] };
				dd.SetFingerName(toeTextIds[i]);
				footSelects.Add(dd);
			}

			// ---- 右足区切り行 ----
			var divider = new VisualElement();
			divider.AddToClassList("mdn-hand-divider");

			var rightFootDivLabel = new LocalizedLabel { TextId = "window.right_foot" };
			rightFootDivLabel.AddToClassList("mdn-finger-name-col");
			rightFootDivLabel.AddToClassList("mdn-col-header");
			divider.Add(rightFootDivLabel);

			footSelects.Add(divider);

			// ---- 右足 5本 ----
			string[] rightToes = { "right-foot-thumb", "right-foot-index", "right-foot-middle", "right-foot-ring", "right-foot-little" };
			for (int i = 0; i < 5; i++)
			{
				var dd = new NailDesignDropDowns { name = rightToes[i] };
				dd.SetFingerName(toeTextIds[i]);
				footSelects.Add(dd);
			}
		}

		private void InitializeNailDesignDropDowns()
		{
			var allDropdowns = new NailDesignDropDowns?[] {
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

			// null除去前に指インデックスを設定（配列インデックス = 指インデックス）
			for (int i = 0; i < allDropdowns.Length; i++)
				allDropdowns[i]?.SetFingerIndex(i);

			this._nailDesignDropDowns = allDropdowns.Where(d => d != null).ToArray()!;

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

			// ハンド/フットセクションヘッダーを保存（エラーハイライト用）
			this._handSectionHeader = this._tglHandActive?.parent;
			this._footSectionHeader = this._tglFootActive?.parent;

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

					// どちらかがONになったらエラーハイライトとバナーを解除
					if (isActive || (this._tglFootActive?.value ?? false))
					{
						this.ClearHandFootError();
						if (this._errorMessage?.text == S("error.execute.no_target"))
							this.HideErrorBanner();
					}
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

					// どちらかがONになったらエラーハイライトとバナーを解除
					if (isActive || (this._tglHandActive?.value ?? false))
					{
						this.ClearHandFootError();
						if (this._errorMessage?.text == S("error.execute.no_target"))
							this.HideErrorBanner();
					}
				}

				_tglFootActive.RegisterValueChangedCallback(_ => UpdateFootVisibility());
				_tglFootDetail.RegisterValueChangedCallback(_ => UpdateFootVisibility());
				UpdateFootVisibility();
			}
		}

		private void OnChangeAvatarSortOrder(AvatarSortOrder order) { this._avatarDropDowns?.Sort(order); }

		private void OnChangeAvatar(ChangeEvent<Object> evt)
		{
			if (evt.newValue is VRCAvatarDescriptor avatar)
			{
				// アバターが設定されたらエラー枠・バナーを解除
				this.ClearAvatarFieldError();
				this.HideErrorBanner();

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
			ToolConsole.OnLog = null;
		}

		private world.anlabo.mdnailtool.Editor.Window.Controllers.MDNailScenePreviewController? _scenePreviewController;


		private void OnExecute()
		{
			ToolConsole.Log("=== OnExecute 開始 ===");
			this.CleanupScenePreview();
			this._nailPreviewController?.CleanupAdditionalObjects();
			this.HideErrorBanner();

			// ---- Validation ----
			VRCAvatarDescriptor? avatar = this._avatarObjectField!.value as VRCAvatarDescriptor;
			if (avatar == null)
			{
				this.ShowErrorBanner(S("dialog.error.select_target_avatar"));
				this.ShowAvatarFieldError();
				return;
			}
			this.ClearAvatarFieldError();

			bool isHandActive = this._tglHandActive?.value ?? true;
			bool isFootActive = this._tglFootActive?.value ?? false;

			if (!isHandActive && !isFootActive)
			{
				this.ShowErrorBanner(S("error.execute.no_target"));
				this.ShowHandFootError();
				return;
			}
			this.ClearHandFootError();

			AvatarVariation? avatarVariationData = this._avatarDropDowns!.GetSelectedAvatarVariation();
			if (avatarVariationData == null)
			{
				this.ShowErrorBanner(S("error.execute.no_avatar_variation"));
				return;
			}

			GameObject? prefab = this._avatarDropDowns!.GetSelectedPrefab();
			if (prefab == null)
			{
				this.ShowErrorBanner(S("error.execute.no_prefab"));
				return;
			}

			string? nailShapeName = this._nailShapeDropDown!.value;
			if (nailShapeName == null)
			{
				this.ShowErrorBanner(S("error.execute.no_nail_shape"));
				return;
			}

			// ---- Process ----
			AssetDatabase.StartAssetEditing();
			try
			{
				(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();

				Mesh?[]? selectedMeshes = this._nailShapeDropDown!.GetSelectedShapeMeshes();
				Mesh?[]? overrideMesh = isHandActive ? selectedMeshes : new Mesh?[10];

				Material? directMaterial = this.GetDirectMaterial();

				// BlendShapeVariant ドロップダウンの状態をログ
				{
					var bsPopup = this._avatarDropDowns?.BlendShapeVariantPopup;
					ToolConsole.Log($"  BlendShapeVariantPopup: null?={bsPopup == null}, index={bsPopup?.index ?? -1}, value={bsPopup?.value ?? "(null)"}, enabled={bsPopup?.enabledSelf}, choices=[{string.Join(", ", bsPopup?.choices ?? new List<string>())}]");
					ToolConsole.Log($"  MA={this._forModularAvatar?.value}, BakeBS={this._bakeBlendShapes?.value}");
				}

				NailSetupProcessor processor = new(avatar, avatarVariationData, prefab, designAndVariationNames, nailShapeName)
				{
					AvatarName = this._avatarDropDowns?.GetAvatarName(),
					OverrideMesh = overrideMesh,
					UseFootNail = isFootActive,
					RemoveCurrentNail = this._removeCurrentNail!.value,
					GenerateMaterial = directMaterial == null,  // 直接指定時は生成OFF
					Backup = this._backup!.value,
					ForModularAvatar = this._forModularAvatar!.value,
					OverrideMaterial = directMaterial,
					GenerateExpressionMenu = (this._forModularAvatar?.value == true)
					                      && (this._generateExpressionMenu?.value == true),
					SplitHandFoot = (this._forModularAvatar?.value == true)
					             && (this._generateExpressionMenu?.value == true)
					             && (this._splitHandFootExpressionMenu?.value == true),
					MergeAnLabo = (this._forModularAvatar?.value == true)
					           && (this._generateExpressionMenu?.value == true)
					           && (this._mergeAnLaboExpressionMenu?.value == true),
					ArmatureScaleCompensation = (this._armatureScaleCompensation?.value == true),
					BakeBlendShapes = (this._forModularAvatar?.value == true)
					               && (this._bakeBlendShapes?.value == true),
					SyncBlendShapesWithMA = (this._forModularAvatar?.value == true)
					                     && (this._bakeBlendShapes?.value == true)
					                     && (this._syncBlendShapesWithMA?.value == true),
					SelectedBlendShapeVariantName = (!(this._forModularAvatar?.value == true && this._bakeBlendShapes?.value == true) && this._avatarDropDowns?.BlendShapeVariantPopup != null && this._avatarDropDowns.BlendShapeVariantPopup.index > 0) ? this._avatarDropDowns.BlendShapeVariantPopup.value : null,
					EnablePenetrationCorrection = (this._penetrationCorrection?.value == true),
					EnableAdditionalMaterials = true,
					PerFingerAdditionalMaterials = this.BuildPerFingerAdditionalMaterials(false),
					PerFingerAdditionalObjects = this.BuildPerFingerAdditionalObjects(false),
				};

				// ToolConsole: PerFingerAdditionalObjects の状態をログ
				ToolConsole.Log($"  PerFingerAdditionalObjects null? {processor.PerFingerAdditionalObjects == null}");
				if (processor.PerFingerAdditionalObjects != null)
				{
					for (int i = 0; i < processor.PerFingerAdditionalObjects.Length; i++)
						ToolConsole.Log($"    finger[{i}]: {(processor.PerFingerAdditionalObjects[i] != null ? "あり" : "null")}");
				}

				// AvatarEntityをprocessorにセット（shop.jsonのblendShapeVariantsを参照するため）
				{
					using DBShop dbShop = new();
					string avatarName = this._avatarDropDowns!.GetAvatarName();
					foreach (Shop s in dbShop.collection)
					{
						Avatar? av = s.FindAvatarByName(avatarName);
						if (av != null)
						{
							processor.AvatarEntity = av;
							break;
						}
					}
				}

				ToolConsole.Log("  processor.Process() 開始");
				processor.Process();
				ToolConsole.Log("  processor.Process() 完了");

				if (!isHandActive) this.RemoveHandNailObjects(avatar);
				if (!isFootActive) this.RemoveFootNailObjects(avatar);

				string avatarKey = this._avatarDropDowns!.GetAvatarKey();
				MDNailToolUsageStats.Update(
					designAndVariationNames,
					avatarKey,
					nailShapeName: nailShapeName,
					isHandActive: isHandActive,
					isFootActive: isFootActive,
					useModularAvatar: this._forModularAvatar?.value == true,
					additionalMaterialSource: GlobalSetting.AdditionalMaterialSourceDesign,
					additionalObjectSource: GlobalSetting.AdditionalObjectSourceDesign);
				this._nailDesignSelect!.Init();

				this.HideErrorBanner();
				if (processor.Warnings.Count > 0)
				{
					string warnSummary = string.Format(
						S("warning.variant_load_failed") ?? "一部のバリアントの読み込みに失敗しました ({0}件)",
						processor.Warnings.Count);
					this.ShowWarningBanner(warnSummary, processor.Warnings);
				}
				else
				{
					this.HideWarningBanner();
				}
				string successMessage = BuildSuccessMessage(isHandActive, isFootActive);
				EditorUtility.DisplayDialog(S("dialog.finished"), successMessage, "OK");
			}
			catch (NailSetupUserException e)
			{
				Debug.LogWarning($"[MDNailTool] {e.Message}\n{e.StackTrace}");
				this.ShowErrorBanner(e.Message);
				this._userErrorCount++;
				if (this._userErrorCount >= 2) {
					this.ShowContactLinks(e.ToString());
				}
			}
			catch (Exception e)
			{
				Debug.LogError(e);
				this.ShowErrorBanner(S("error.execute.unexpected"), e);
				this.ShowContactLinks(e.ToString());
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}
		}

		private static string BuildSuccessMessage(bool handActive, bool footActive)
		{
			string handLabel = S("window.hand_nail") ?? "Hand Nail";
			string footLabel = S("window.foot_nail") ?? "Foot Nail";
			string suffix    = S("dialog.finished.attached_suffix") ?? " attachment is complete.";

			string target = (handActive, footActive) switch {
				(true,  true)  => handLabel + " " + (S("dialog.finished.and") ?? "&") + " " + footLabel,
				(true,  false) => handLabel,
				(false, true)  => footLabel,
				_              => ""
			};
			return target + suffix;
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
			this.HideErrorBanner();
			this.HideWarningBanner();

			VRCAvatarDescriptor? avatar = this._avatarObjectField!.value as VRCAvatarDescriptor;
			if (avatar == null)
			{
				this.ShowErrorBanner(S("dialog.error.select_target_avatar"));
				this.ShowAvatarFieldError();
				return;
			}
			this.ClearAvatarFieldError();

			AvatarVariation? avatarVariationData = this._avatarDropDowns!.GetSelectedAvatarVariation();
			if (avatarVariationData != null) NailSetupProcessor.RemoveNail(avatar, avatarVariationData.BoneMappingOverride);
		}

		private void OnSelectNail(string designName)
		{
			ResourceAutoExtractor.EnsureDesignExtracted(designName);
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

			// 追加マテリアル・追加オブジェクトのデフォルトをプレビュー前に設定
			this.UpdateAdditionalMaterialSourceDefault();
			this.UpdateAdditionalObjectSourceDefault();

			this.UpdateNailShapeFilter(nailProcessor);
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();

			// バリアントドロップダウンを更新（マテリアルバリエーション欄と入れ替え表示）
			string? parentName = design.ParentVariant;
			string parentDesignName = !string.IsNullOrEmpty(parentName) ? parentName! : designName;
			IReadOnlyList<NailDesign> variantChildren = dbNailDesign.FindChildVariants(parentDesignName);
			if (variantChildren.Count > 0 && this._nailVariantDropDown != null)
			{
				string langKey = CurrentLanguageData.language;
				this._variantDisplayNames = new Dictionary<string, string>();
				var variantChoices = new List<string>();

				// 親
				NailDesign? parentDesign = dbNailDesign.FindNailDesignByDesignName(parentDesignName);
				if (parentDesign != null)
				{
					string pDisplay = parentDesign.DisplayNames?.GetValueOrDefault(langKey, parentDesign.DesignName) ?? parentDesign.DesignName;
					this._variantDisplayNames[parentDesignName] = pDisplay;
					variantChoices.Add(parentDesignName);
				}

				// 子
				foreach (NailDesign child in variantChildren)
				{
					string cDisplay = child.DisplayNames?.GetValueOrDefault(langKey, child.DesignName) ?? child.DesignName;
					this._variantDisplayNames[child.DesignName] = cDisplay;
					variantChoices.Add(child.DesignName);
				}

				var displayMap = this._variantDisplayNames;
				this._nailVariantDropDown.choices = variantChoices;
				this._nailVariantDropDown.SetValueWithoutNotify(designName);
				this._nailVariantDropDown.formatListItemCallback = name =>
					name != null && displayMap.TryGetValue(name, out string? dn) ? dn : name ?? "";
				this._nailVariantDropDown.formatSelectedValueCallback = name =>
					name != null && displayMap.TryGetValue(name, out string? dn) ? dn : name ?? "";

				// マテリアルドロップダウンを隠し、バリアントドロップダウンを表示
				this._nailMaterialDropDown!.style.display = DisplayStyle.None;
				this._nailVariantDropDown.style.display = DisplayStyle.Flex;
			}
			else
			{
				// バリアントなし → マテリアルドロップダウンを表示、バリアントを隠す
				this._nailMaterialDropDown!.style.display = DisplayStyle.Flex;
				if (this._nailVariantDropDown != null)
				{
					this._nailVariantDropDown.style.display = DisplayStyle.None;
				}
			}
		}

		private void OnChangeNailVariantDropDown(ChangeEvent<string> evt)
		{
			if (string.IsNullOrEmpty(evt.newValue)) return;
			this.OnSelectNail(evt.newValue);
		}

		private void OnChangeEnableDirectMaterial(ChangeEvent<bool> evt)
		{
			if (this._materialObjectField != null)
			{
				this._materialObjectField.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
			}
			this.UpdateNailShapeFilter();
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
		}

		private Material? GetDirectMaterial()
		{
			if (this._enableDirectMaterial != null && !this._enableDirectMaterial.value) return null;
			return this._materialObjectField?.value as Material;
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
			if (this.GetDirectMaterial() != null) { this._nailShapeDropDown!.SetFilter(_ => true); return; }
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

			Material? directMaterial = this.GetDirectMaterial();

			var perFingerAddMats = this.BuildPerFingerAdditionalMaterials(true);
			var perFingerAddObjs = this.BuildPerFingerAdditionalObjects(true);

			this._nailPreviewController?.ChangeNailMaterial(designAndVariationNames, nailShapeName, directMaterial,
				true, perFingerAddMats);
			this._nailPreviewController?.ChangeAdditionalObjects(designAndVariationNames, nailShapeName, perFingerAddObjs);
		}

		private void RequestScenePreviewUpdate()
		{
			if (!GlobalSetting.EnableSceneWearingPreview) return;

			this._scenePreviewSchedule?.Pause();
			this._scenePreviewSchedule = this.rootVisualElement.schedule
				.Execute(() => this.UpdateScenePreview(immediate: true))
				.StartingIn(SCENE_PREVIEW_DEBOUNCE_MS);
		}

		private void UpdateScenePreview(bool immediate)
		{
			if (!GlobalSetting.EnableSceneWearingPreview) return;

			var avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			if (avatar == null) return;

			var prefab = this._avatarDropDowns?.GetSelectedPrefab();
			if (prefab == null) return;

			string nailShapeName = this._nailShapeDropDown?.value ?? GlobalSetting.LastUseShapeName ?? "oval";
			if (string.IsNullOrEmpty(nailShapeName)) nailShapeName = "oval";
			if (string.IsNullOrEmpty(nailShapeName)) return;

			// BakeBS OFF: バリアント選択時はプレハブを丸ごと差し替え
			bool bakeBS = this._bakeBlendShapes?.value == true && this._forModularAvatar?.value == true;
			if (!bakeBS)
			{
				var bsPopup = this._avatarDropDowns?.BlendShapeVariantPopup;
				if (bsPopup != null && bsPopup.index > 0)
				{
					GameObject? variantPrefab = this.ResolveVariantPrefabForPreview(bsPopup.value);
					if (variantPrefab != null)
					{
						prefab = variantPrefab;
					}
				}
			}

			Mesh?[]? overrideMeshes = this._nailShapeDropDown?.GetSelectedShapeMeshes();
			if (overrideMeshes == null) overrideMeshes = new Mesh?[0];

			bool isHandActive = this._tglHandActive?.value ?? true;
			bool isFootActive = this._tglFootActive?.value ?? false;

			(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();
			Material? directMaterial = this.GetDirectMaterial();

			var perFingerAddMats = this.BuildPerFingerAdditionalMaterials(true);
			var perFingerAddObjs = this.BuildPerFingerAdditionalObjects(true);

			bool armatureCompensation = this._armatureScaleCompensation?.value == true;

			this._scenePreviewController ??= new MDNailScenePreviewController(SCENE_PREVIEW_NAME);
			this._scenePreviewController.Update(
				avatar,
				prefab,
				overrideMeshes,
				nailShapeName,
				isHandActive,
				isFootActive,
				designAndVariationNames,
				directMaterial,
				true,
				perFingerAddMats,
				perFingerAddObjs,
				armatureCompensation
			);

			// BakeBS ON: 体のBlendShape値に基づいてネイル位置を補間
			if (bakeBS)
			{
				this.ApplyVariantPositionBlend(avatar, prefab, nailShapeName);
			}
		}


		private void CleanupScenePreview()
		{
			this._scenePreviewController?.Cleanup(this._avatarObjectField?.value as VRCAvatarDescriptor);
			this._scenePreviewController = null;
		}

		/// <summary>
		/// BlendShapeバリアント名からバリアントプレハブを解決する（着用プレビュー用）。
		/// 選択中のネイルシェイプに対応するプレハブまで解決して返す。
		/// </summary>
		private GameObject? ResolveVariantPrefabForPreview(string variantName)
		{
			AvatarBlendShapeVariant[]? variants = this.GetBlendShapeVariants();
			if (variants == null) return null;

			AvatarBlendShapeVariant? variant = variants.FirstOrDefault(v => v.Name == variantName);
			if (variant == null || string.IsNullOrEmpty(variant.NailPrefabGUID)) return null;

			string path = AssetDatabase.GUIDToAssetPath(variant.NailPrefabGUID);
			if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
			{
				ResourceAutoExtractor.EnsurePrefabExtractedByGuid(variant.NailPrefabGUID);
				AssetDatabase.Refresh();
				path = AssetDatabase.GUIDToAssetPath(variant.NailPrefabGUID);
			}
			if (string.IsNullOrEmpty(path)) return null;

			GameObject? variantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (variantPrefab == null) return null;

			string nailShapeName = this._nailShapeDropDown?.value ?? GlobalSetting.LastUseShapeName ?? "oval";
			if (!string.IsNullOrEmpty(nailShapeName))
			{
				variantPrefab = NailSetupProcessor.ResolveShapePrefab(variantPrefab, nailShapeName);
			}

			return variantPrefab;
		}

		/// <summary>
		/// BakeBS ON時: 体のBlendShape値に基づいてプレビューネイルの位置を補間する。
		/// 各バリアントの体BlendShape重みを読み取り、ベースとバリアントの位置デルタを加算する。
		/// </summary>
		private void ApplyVariantPositionBlend(VRCAvatarDescriptor avatar, GameObject basePrefab, string nailShapeName)
		{
			Transform? previewRoot = avatar.transform.Find(SCENE_PREVIEW_NAME);
			if (previewRoot == null) return;

			AvatarBlendShapeVariant[]? variants = this.GetBlendShapeVariants();
			if (variants == null || variants.Length == 0) return;

			var previewTransforms = previewRoot.GetComponentsInChildren<Transform>(true);
			var baseTransforms = basePrefab.GetComponentsInChildren<Transform>(true);

			foreach (var variant in variants)
			{
				if (string.IsNullOrEmpty(variant.SyncSourceSmrName)) continue;

				// 体のSMRからBlendShape重みを取得
				float weight = GetBodyBlendShapeWeight(avatar, variant);
				if (weight <= 0f) continue;

				// バリアントプレハブをロード
				if (string.IsNullOrEmpty(variant.NailPrefabGUID)) continue;
				string varPath = AssetDatabase.GUIDToAssetPath(variant.NailPrefabGUID);
				if (string.IsNullOrEmpty(varPath)) continue;
				GameObject? variantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(varPath);
				if (variantPrefab == null) continue;

				variantPrefab = NailSetupProcessor.ResolveShapePrefab(variantPrefab, nailShapeName);
				var variantTransforms = variantPrefab.GetComponentsInChildren<Transform>(true);

				// 各ネイルの位置・回転デルタを補間適用
				foreach (Transform previewNail in previewTransforms)
				{
					if (previewNail == previewRoot) continue;

					Transform? baseNail = System.Array.Find(baseTransforms, t => t.name == previewNail.name);
					Transform? variantNail = System.Array.Find(variantTransforms, t => t.name == previewNail.name);
					if (baseNail == null || variantNail == null) continue;

					Vector3 posDelta = variantNail.localPosition - baseNail.localPosition;
					previewNail.localPosition += weight * posDelta;

					Quaternion rotDelta = variantNail.localRotation * Quaternion.Inverse(baseNail.localRotation);
					previewNail.localRotation = Quaternion.Slerp(Quaternion.identity, rotDelta, weight) * previewNail.localRotation;
				}
			}
		}

		/// <summary>バリアント一覧を取得する共通メソッド</summary>
		private AvatarBlendShapeVariant[]? GetBlendShapeVariants()
		{
			var avatarVariationData = this._avatarDropDowns?.GetSelectedAvatarVariation();
			if (avatarVariationData == null) return null;

			AvatarBlendShapeVariant[]? variants = avatarVariationData.BlendShapeVariants;
			if (variants == null)
			{
				using DBShop dbShop = new();
				string avatarName = this._avatarDropDowns!.GetAvatarName();
				foreach (Shop s in dbShop.collection)
				{
					Avatar? av = s.FindAvatarByName(avatarName);
					if (av?.BlendShapeVariants != null)
					{
						variants = av.BlendShapeVariants;
						break;
					}
				}
			}
			return variants;
		}

		/// <summary>体のSMRからバリアントに対応するBlendShapeの重み（0〜1）を取得する</summary>
		private static float GetBodyBlendShapeWeight(VRCAvatarDescriptor avatar, AvatarBlendShapeVariant variant)
		{
			Transform? srcSmrTransform = FindSyncSourceSmr(avatar, variant.SyncSourceSmrName!);
			if (srcSmrTransform == null) return 0f;

			SkinnedMeshRenderer? srcSmr = srcSmrTransform.GetComponent<SkinnedMeshRenderer>();
			if (srcSmr == null || srcSmr.sharedMesh == null) return 0f;

			string normalizedName = variant.Name.Replace(" ", "").Replace("　", "");
			for (int i = 0; i < srcSmr.sharedMesh.blendShapeCount; i++)
			{
				string bsName = srcSmr.sharedMesh.GetBlendShapeName(i);
				if (bsName.Replace(" ", "").Replace("　", "") == normalizedName)
				{
					return srcSmr.GetBlendShapeWeight(i) / 100f;
				}
			}
			return 0f;
		}

		/// <summary>SyncSourceSmrNameからアバター上のSMR Transformを検索する（NailSetupProcessorと同じフォールバックロジック）</summary>
		private static Transform? FindSyncSourceSmr(VRCAvatarDescriptor avatar, string syncSourceSmrName)
		{
			var allTransforms = avatar.transform.GetComponentsInChildren<Transform>(true);

			// Step 1: 名前完全一致
			Transform? t = System.Array.Find(allTransforms, tr => tr.name == syncSourceSmrName);
			if (t != null) return t;

			// Step 2: 大文字小文字無視
			t = System.Array.Find(allTransforms, tr => string.Equals(tr.name, syncSourceSmrName, System.StringComparison.OrdinalIgnoreCase));
			if (t != null) return t;

			// Step 3: 部分一致
			t = System.Array.Find(allTransforms, tr => tr.GetComponent<SkinnedMeshRenderer>() != null
				&& (tr.name.Contains(syncSourceSmrName) || syncSourceSmrName.Contains(tr.name)));
			if (t != null) return t;

			// Step 4: BlendShapeを持つ非顔SMRから推測
			SkinnedMeshRenderer? visemeSmr = avatar.VisemeSkinnedMesh;
			return avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
				.Where(smr => smr != visemeSmr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
				.OrderByDescending(smr => smr.sharedMesh!.blendShapeCount)
				.FirstOrDefault()?.transform;
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
		private void OnChangeForModularAvatar(ChangeEvent<bool> evt)
		{
			GlobalSetting.UseModularAvatar = evt.newValue;
			this.UpdateMASubOptionsVisibility(evt.newValue);
			this.UpdateBlendShapeVariantVisibility();
		}

		private void UpdateMASubOptionsVisibility(bool useMA)
		{
			var maSubOptions = this.rootVisualElement.Q<VisualElement>("ma-sub-options");
			if (maSubOptions != null)
			{
				maSubOptions.style.display = useMA ? DisplayStyle.Flex : DisplayStyle.None;
			}
			if (this._generateExpressionMenu != null)
			{
				this._generateExpressionMenu.SetEnabled(useMA);
				this._splitHandFootExpressionMenu?.SetEnabled(useMA && this._generateExpressionMenu.value);
				this._mergeAnLaboExpressionMenu?.SetEnabled(useMA && this._generateExpressionMenu.value);
			}
			this._bakeBlendShapes?.SetEnabled(useMA);
			this._syncBlendShapesWithMA?.SetEnabled(useMA && (this._bakeBlendShapes?.value == true));
		}
		private void ShowAvatarSearchWindow() { SearchAvatarWindow.ShowWindow(this); }
		private void ShowNailSearchWindow() { SearchNailDesignWindow.ShowWindow(this); }
		public void SelectNailFromSearch(string designName) { this.OnSelectNail(designName); }

		// プレビューウィンドウ（NailPreview）の表示/非表示
		private void OnChangePreviewWindowVisible(ChangeEvent<bool> evt)
		{
			GlobalSetting.EnableScenePreview = evt.newValue;
			this.UpdatePreviewAreaVisibility(evt.newValue);
		}

		// 着用プレビュー（Sceneへの仮着用）のON/OFF
		private void OnChangeEnableScenePreview(ChangeEvent<bool> evt)
		{
			GlobalSetting.EnableSceneWearingPreview = evt.newValue;

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
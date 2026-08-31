#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Core;
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

namespace world.anlabo.mdnailtool.Editor.Window
{
	public partial class MDNailToolWindow
	{
		private void BindCoreFields()
		{
			Toggle? showTermHelp = this.rootVisualElement.Q<Toggle>("show-term-help");
			if (showTermHelp != null)
			{
				showTermHelp.SetValueWithoutNotify(GlobalSetting.ShowTermHelp);
				showTermHelp.RegisterValueChangedCallback(evt =>
				{
					GlobalSetting.ShowTermHelp = evt.newValue;
					this.UpdateTermHelpVisibility();
				});
				this.rootVisualElement.Q<LocalizedLabel>("label-show-term-help")?.RegisterCallback<ClickEvent>(_ =>
					showTermHelp.value = !showTermHelp.value);
			}
			this.InstallTermHelpButtons();

			this._enableBetaFeatures = this.rootVisualElement.Q<Toggle>("enable-beta-features");
			this._betaFeaturesArea = this.rootVisualElement.Q<VisualElement>("beta-features-area");
			if (this._enableBetaFeatures != null)
			{
				this._enableBetaFeatures.SetValueWithoutNotify(GlobalSetting.EnableBetaFeatures);
				this._enableBetaFeatures.RegisterValueChangedCallback(evt =>
				{
					GlobalSetting.EnableBetaFeatures = evt.newValue;
					this.UpdateBetaFeaturesVisibility();
				});
				var betaLabel = this.rootVisualElement.Q<LocalizedLabel>("label-enable-beta-features");
				betaLabel?.RegisterCallback<ClickEvent>(_ =>
					this._enableBetaFeatures.value = !this._enableBetaFeatures.value);
			}
			this.UpdateBetaFeaturesVisibility();

			this._enableDirectMaterial = this.rootVisualElement.Q<Toggle>("enable-direct-material");
			this._directMaterialToggleRow = this.rootVisualElement.Q<VisualElement>("direct-material-row");

			// ObjectField を C# 側で生成してトグル直下の行に追加
			this._directMaterialFieldRow = this.rootVisualElement.Q<VisualElement>("direct-material-field-row");
			this._materialObjectField = new ObjectField {
				name = "material-object",
				label = S("window.direct_material_field") ?? "Material",
				objectType = typeof(Material),
				style = {
					flexGrow = 1,
					flexShrink = 1,
				}
			};
			this._materialObjectField.RegisterValueChangedCallback(this.OnChangeMaterial);
			this._directMaterialFieldRow?.Add(this._materialObjectField);

			this._customNailTextureRow = this.rootVisualElement.Q<VisualElement>("custom-nail-texture-row");
			this._customNailTextureSelect = new DropdownField { label = S("window.custom_nail_texture") ?? "Custom nail texture" };
			this._customNailTextureSelect.AddToClassList("mdn-custom-nail-texture-select");
			this._customNailTextureSelect.RegisterValueChangedCallback(this.OnChangeCustomNailTexture);
			this._customNailTextureRow?.Add(this._customNailTextureSelect);
			Button refreshCustomNails = new(this.RebuildCustomNailTextures) { text = "↻" };
			refreshCustomNails.AddToClassList("mdn-shader-preset-btn");
			refreshCustomNails.tooltip = S("window.custom_nail_refresh") ?? "Reload textures";
			this._customNailTextureRow?.Add(refreshCustomNails);
			Button pingCustomNailFolder = new(() =>
			{
				CustomNailTextureService.EnsureFolder();
				UnityEngine.Object? folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MDNailToolDefines.CUSTOM_NAIL_TEXTURE_PATH.TrimEnd('/'));
				if (folder == null) return;
				Selection.activeObject = folder;
				EditorGUIUtility.PingObject(folder);
			});
			pingCustomNailFolder.AddToClassList("mdn-shader-preset-btn");
			pingCustomNailFolder.AddToClassList("mdn-shader-preset-btn-folder");
			pingCustomNailFolder.tooltip = S("window.custom_nail_ping_folder") ?? "Show folder in Project";
			this._customNailTextureRow?.Add(pingCustomNailFolder);
			Button openCustomNailFolder = new(() =>
			{
				CustomNailTextureService.EnsureFolder();
				EditorUtility.RevealInFinder(MDNailToolDefines.CUSTOM_NAIL_TEXTURE_PATH);
			}) { text = "…" };
			openCustomNailFolder.AddToClassList("mdn-shader-preset-btn");
			openCustomNailFolder.tooltip = S("window.custom_nail_open_folder") ?? "Open folder";
			this._customNailTextureRow?.Add(openCustomNailFolder);
			this.RebuildCustomNailTextures();

			if (this._enableDirectMaterial != null)
			{
				using DBNailDesign nailDesignDb = new();
				bool hasInstalledAnLaboNail = nailDesignDb.collection.Any(nailDesignDb.IsInstalledDesignGroup);
				bool directMaterialEnabled = GlobalSetting.HasDirectMaterialPreference
					? GlobalSetting.DirectMaterialEnabled
					: !hasInstalledAnLaboNail;
				this._enableDirectMaterial.SetValueWithoutNotify(directMaterialEnabled);
				if (this._directMaterialFieldRow != null)
					this._directMaterialFieldRow.style.display = directMaterialEnabled ? DisplayStyle.Flex : DisplayStyle.None;
				if (this._customNailTextureRow != null)
					this._customNailTextureRow.style.display = directMaterialEnabled && GlobalSetting.EnableBetaFeatures ? DisplayStyle.Flex : DisplayStyle.None;
				this._enableDirectMaterial.RegisterValueChangedCallback(this.OnChangeEnableDirectMaterial);
				var lblEnableDirectMat = this.rootVisualElement.Q<LocalizedLabel>("label-enable-direct-material");
				lblEnableDirectMat?.RegisterCallback<ClickEvent>(_ => {
					if (this._enableDirectMaterial != null) this._enableDirectMaterial.value = !this._enableDirectMaterial.value;
				});
			}

			this._avatarObjectField = this.rootVisualElement.Q<LocalizedObjectField>("avatar-object");
			this._avatarObjectField.RegisterValueChangedCallback(this.OnChangeAvatar);
		}

		private void InstallTermHelpButtons()
		{
			(string buttonName, string helpId)[] terms =
			{
				("term-help-shader-preset", "term_help.shader_preset"),
				("term-help-modular-avatar", "term_help.modular_avatar"),
				("term-help-expression-menu", "term_help.expression_menu"),
				("term-help-bake-blendshapes", "term_help.blendshape"),
				("term-help-shrink-blendshape", "term_help.blendshape"),
			};

			foreach ((string buttonName, string helpId) in terms)
			{
				Button? helpButton = this.rootVisualElement.Q<Button>(buttonName);
				if (helpButton != null) helpButton.tooltip = S(helpId) ?? "";
			}

			this.UpdateTermHelpVisibility();
		}

		private void UpdateTermHelpVisibility()
		{
			this.rootVisualElement.Query<Button>(className: "mdn-term-help-button").ForEach(button =>
				button.style.display = GlobalSetting.ShowTermHelp ? DisplayStyle.Flex : DisplayStyle.None);
		}

		private void UpdateBetaFeaturesVisibility()
		{
			bool betaEnabled = this._enableBetaFeatures?.value == true;
			if (this._betaFeaturesArea != null)
				this._betaFeaturesArea.style.display =
					betaEnabled ? DisplayStyle.Flex : DisplayStyle.None;
			if (this._directMaterialToggleRow != null)
				this._directMaterialToggleRow.style.display = betaEnabled ? DisplayStyle.None : DisplayStyle.Flex;
			this._nailDesignSelect?.Init();
			if (this._customNailTextureRow != null)
				this._customNailTextureRow.style.display = betaEnabled && this._enableDirectMaterial?.value == true
					? DisplayStyle.Flex : DisplayStyle.None;
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
			this._nailDesignSelect.OnSelectExternalNail += this.OnSelectExternalNail;
			this._nailDesignSelect.OnSearchButtonClicked += this.ShowNailSearchWindow;
			// UXML生成時より後に復元されたベータ設定を反映し、再起動時も先頭カードを再構築する。
			this.UpdateBetaFeaturesVisibility();

			this._nailPreview = this.rootVisualElement.Q<NailPreview>("nail-preview");
			this._nailPreviewController = new NailPreviewController(this._nailPreview);

			this._nailShapeDropDown = this.rootVisualElement.Q<NailShapeDropDown>("nail-shape");
			this._nailShapeDropDown.SetNailShape(GlobalSetting.LastUseShapeName);
			this._nailShapeDropDown.RegisterValueChangedCallback(this.OnChangeShapeDropDown);
			NailDesignDropDowns.AddArrowKeyNavigation(this._nailShapeDropDown);
			NailDesignDropDowns.UseScrollablePopup(this._nailShapeDropDown);

			this._nailMaterialDropDown = this.rootVisualElement.Q<LocalizedDropDown>("nail-material");
			this._nailMaterialDropDown.RegisterValueChangedCallback(this.OnChangeNailMaterialDropDown);
			NailDesignDropDowns.AddArrowKeyNavigation(this._nailMaterialDropDown);
			NailDesignDropDowns.UseScrollablePopup(this._nailMaterialDropDown);

			this._nailColorDropDown = this.rootVisualElement.Q<LocalizedDropDown>("nail-color");
			this._nailColorDropDown.RegisterValueChangedCallback(this.OnChangeNailColorDropDown);
			NailDesignDropDowns.AddArrowKeyNavigation(this._nailColorDropDown);
			NailDesignDropDowns.UseScrollablePopup(this._nailColorDropDown);

			// バリアント選択ドロップダウンをマテリアルバリエーション欄に配置（初期は非表示）
			// バリアントがあるデザインの場合、マテリアルドロップダウンを隠してこちらを表示する
			this._nailVariantDropDown = new DropdownField { label = "バリアント" };
			this._nailVariantDropDown.AddToClassList("mdn-style-dropdown");
			this._nailVariantDropDown.style.display = DisplayStyle.None;
			this._nailVariantDropDown.RegisterValueChangedCallback(this.OnChangeNailVariantDropDown);
			NailDesignDropDowns.AddArrowKeyNavigation(this._nailVariantDropDown);
			NailDesignDropDowns.UseScrollablePopup(this._nailVariantDropDown);
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

			// 着用時にウィンドウを閉じる
			this._closeWindowOnExecute = this.rootVisualElement.Q<Toggle>("close-window-on-execute");
			if (this._closeWindowOnExecute != null)
			{
				this._closeWindowOnExecute.SetValueWithoutNotify(GlobalSetting.CloseWindowOnExecute);
				this._closeWindowOnExecute.RegisterValueChangedCallback(evt => GlobalSetting.CloseWindowOnExecute = evt.newValue);
			}
			var lblCloseWindow = this.rootVisualElement.Q<LocalizedLabel>("label-close-window-on-execute");
			lblCloseWindow?.RegisterCallback<ClickEvent>(_ => { if (this._closeWindowOnExecute != null) this._closeWindowOnExecute.value = !this._closeWindowOnExecute.value; });

			// プレビュー（常時ON、ヘッダー非表示）
			this._enableScenePreview = this.rootVisualElement.Q<Toggle>("enable-scene-preview");
			if (this._enableScenePreview != null)
			{
				this._enableScenePreview.SetValueWithoutNotify(true);
				this._enableScenePreview.parent.style.display = DisplayStyle.None;
				this.UpdatePreviewAreaVisibility(true);
			}

			// 着用プレビュー (シーン試着トグル): 毎回OFFで起動、アクションバーのボタンで切替
			this._tryoutToggle = this.rootVisualElement.Q<Button>("tryout-toggle");
			this._tryoutBanner = this.rootVisualElement.Q<VisualElement>("tryout-banner");
			this._tryoutActive = false;
			GlobalSetting.EnableSceneWearingPreview = false;
			if (this._tryoutToggle != null)
			{
				string? tip = S("tooltip.tryout_toggle");
				if (tip != null) this._tryoutToggle.tooltip = tip;
			}
			this.UpdateTryoutVisual();
			if (this._tryoutToggle != null) this._tryoutToggle.clicked += this.OnToggleTryout;

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

			this._bakeBlendShapeGeneratedList = this.rootVisualElement.Q<Label>("bake-blendshape-generated-list");

			this._bakeBlendShapes = this.rootVisualElement.Q<Toggle>("bake-blendshapes");
			if (this._bakeBlendShapes != null)
			{
				this._bakeBlendShapes.SetValueWithoutNotify(GlobalSetting.BakeBlendShapes);
				this._bakeBlendShapes.RegisterValueChangedCallback(evt => {
					GlobalSetting.BakeBlendShapes = evt.newValue;
					this.UpdateBlendShapeVariantDropDown();
					if (this._autoLinkShrinkBS != null) {
						bool en = GlobalSetting.UseModularAvatar && evt.newValue;
						this._autoLinkShrinkBS.SetEnabled(en);
						if (!en && this._autoLinkShrinkBS.value) this._autoLinkShrinkBS.value = false;
					}
				});
				this._bakeBlendShapes.SetEnabled(GlobalSetting.UseModularAvatar);
				var lblBake = this.rootVisualElement.Q<LocalizedLabel>("label-bake-blendshapes");
				lblBake?.RegisterCallback<ClickEvent>(_ => {
					if (this._bakeBlendShapes != null && this._bakeBlendShapes.enabledSelf)
						this._bakeBlendShapes.value = !this._bakeBlendShapes.value;
				});
			}

			this._autoLinkShrinkBS = this.rootVisualElement.Q<Toggle>("auto-link-shrink-bs");
			if (this._autoLinkShrinkBS != null)
			{
				this._autoLinkShrinkBS.SetValueWithoutNotify(GlobalSetting.AutoLinkShrinkBS);
				this._autoLinkShrinkBS.RegisterValueChangedCallback(evt => {
					GlobalSetting.AutoLinkShrinkBS = evt.newValue;
					this.UpdateBakeBlendShapeGeneratedList();
				});
				this._autoLinkShrinkBS.SetEnabled(GlobalSetting.UseModularAvatar && GlobalSetting.BakeBlendShapes);
				var lblShrink = this.rootVisualElement.Q<LocalizedLabel>("label-auto-link-shrink-bs");
				lblShrink?.RegisterCallback<ClickEvent>(_ => {
					if (this._autoLinkShrinkBS != null && this._autoLinkShrinkBS.enabledSelf)
						this._autoLinkShrinkBS.value = !this._autoLinkShrinkBS.value;
				});
			}

			this.UpdateMASubOptionsVisibility(GlobalSetting.UseModularAvatar);
			this.UpdateBlendShapeVariantDropDown();

			// 追加マテリアルソース選択ドロップダウン
			this._additionalMaterialSourceDropdown = this.rootVisualElement.Q<DropdownField>("additional-material-source");
			if (this._additionalMaterialSourceDropdown != null)
			{
				this.PopulateAdditionalMaterialSourceDropdown();
				NailDesignDropDowns.AddArrowKeyNavigation(this._additionalMaterialSourceDropdown);
				NailDesignDropDowns.UseScrollablePopup(this._additionalMaterialSourceDropdown);
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
				NailDesignDropDowns.UseScrollablePopup(this._additionalObjectSourceDropdown);
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

			// シェーダープリセット
			EnsureShaderPresetUserFolder();
			this._shaderPresetSelect = this.rootVisualElement.Q<DropdownField>("shader-preset-select");
			if (this._shaderPresetSelect != null) NailDesignDropDowns.UseScrollablePopup(this._shaderPresetSelect);
			this._shaderPresetReloadBtn = this.rootVisualElement.Q<Button>("shader-preset-reload");
			this._shaderPresetPingBtn = this.rootVisualElement.Q<Button>("shader-preset-ping");
			this._shaderPresetAddField = this.rootVisualElement.Q<ObjectField>("shader-preset-add-field");
			this._shaderPresetSaveBtn = this.rootVisualElement.Q<Button>("shader-preset-save");
			this._shaderPresetSettingsToggleBtn = this.rootVisualElement.Q<Button>("shader-preset-settings-toggle");
			this._shaderPresetSettingsArea = this.rootVisualElement.Q<VisualElement>("shader-preset-settings-area");
			this._shaderPresetSettingsList = this.rootVisualElement.Q<VisualElement>("shader-preset-settings-list");
			if (this._shaderPresetSelect != null)
			{
				this.RebuildShaderPresetSelect();
				this._shaderPresetSelect.RegisterValueChangedCallback(this.OnChangeShaderPresetSelect);
			}
			if (this._shaderPresetReloadBtn != null) this._shaderPresetReloadBtn.clicked += this.OnClickShaderPresetReload;
			if (this._shaderPresetPingBtn != null) this._shaderPresetPingBtn.clicked += this.OnClickShaderPresetPing;
			if (this._shaderPresetAddField != null) {
				this._shaderPresetAddField.objectType = typeof(Material);
				this._shaderPresetAddField.label = S("window.shader_preset_add_label") ?? "Add Preset";
			}
			if (this._shaderPresetSaveBtn != null) this._shaderPresetSaveBtn.clicked += this.OnClickShaderPresetSave;
			if (this._shaderPresetSettingsToggleBtn != null) this._shaderPresetSettingsToggleBtn.clicked += this.OnClickShaderPresetSettingsToggle;

			// トラブルシューティング (ログ単独. 初期非表示, お問い合わせクリックで表示)
			this._toolConsoleContainer = this.rootVisualElement.Q<VisualElement>("tool-console-container");
			this._toolConsoleScroll = this.rootVisualElement.Q<ScrollView>("tool-console-scroll");

			// ログタイトル設定
			var consoleTitle = this.rootVisualElement.Q<Label>("tool-console-title");
			if (consoleTitle != null) consoleTitle.text = S("window.debug_log_title") ?? "Log";

			// サポート情報コピーボタン
			var copyBtn = this.rootVisualElement.Q<Button>("tool-console-copy");
			if (copyBtn != null) copyBtn.text = S("window.debug_copy") ?? "Copy Support Info";
			copyBtn?.RegisterCallback<ClickEvent>(_ =>
			{
				EditorGUIUtility.systemCopyBuffer = this.BuildSupportInfo();
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
					Material? mat = MDNailToolAssetLoader.LoadAssetSafe<Material>(matPath);
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

			// per-finger ドロップダウンにも同じ選択肢を設定
			this.PopulatePerFingerAdditionalObjectDropdowns(choices);

			// 保存された選択を復元
			string? saved = GlobalSetting.AdditionalObjectSourceDesign;
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
			if (this._nailDesignDropDowns == null)
			{
				return null;
			}

			string? noneLabel = this._additionalObjectSourceDropdown?.choices.FirstOrDefault();
			string? globalValue = this._additionalObjectSourceDropdown?.value;
			string? globalSource = (globalValue == noneLabel) ? null : globalValue;

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
					continue;
				}

				var transforms = new List<Transform>();
				foreach (string resolvedGuid in registry.ResolveObjectGuids(registryName!, i))
				{
					// nodes 埋め込み済なら物理 prefab 不要で復元
					NailPrefabNodeData[]? nodes = null;
					if (registry.Objects != null
					    && registry.Objects.TryGetValue(registryName!, out AdditionalObjectEntry? lookupEntry)
					    && lookupEntry.NodesByGuid != null
					    && lookupEntry.NodesByGuid.TryGetValue(resolvedGuid, out NailPrefabNodeData[]? foundNodes)) {
						nodes = foundNodes;
					}
					if (nodes != null && nodes.Length > 0) {
						GameObject built = NailPrefabBuilder.BuildFromNodes(nodes, $"additional_{resolvedGuid}");
						transforms.Add(built.transform);
						continue;
					}

					string objectPath = AssetDatabase.GUIDToAssetPath(resolvedGuid);
					if (string.IsNullOrEmpty(objectPath))
					{
						ToolConsole.Warn("Window", $"finger[{i}]: AdditionalObject GUID not found: {resolvedGuid} (registryName={registryName})");
						continue;
					}
					GameObject? obj = MDNailToolAssetLoader.LoadPrefabSafe(objectPath);
					if (obj == null)
					{
						ToolConsole.Warn("Window", $"finger[{i}]: AdditionalObject could not load: {objectPath} (registryName={registryName})");
						continue;
					}
					transforms.Add(Object.Instantiate(obj, Vector3.zero, Quaternion.identity).transform);
				}

				if (transforms.Count > 0)
				{
					result[i] = transforms;
					anyNonNull = true;
				}
			}

			return anyNonNull ? result : null;
		}

	}
}

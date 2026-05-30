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

			this._autoLinkShrinkBS = this.rootVisualElement.Q<Toggle>("auto-link-shrink-bs");
			if (this._autoLinkShrinkBS != null)
			{
				this._autoLinkShrinkBS.SetValueWithoutNotify(GlobalSetting.AutoLinkShrinkBS);
				this._autoLinkShrinkBS.RegisterValueChangedCallback(evt => {
					GlobalSetting.AutoLinkShrinkBS = evt.newValue;
				});
				this._autoLinkShrinkBS.SetEnabled(GlobalSetting.UseModularAvatar);
				var lblShrink = this.rootVisualElement.Q<LocalizedLabel>("label-auto-link-shrink-bs");
				lblShrink?.RegisterCallback<ClickEvent>(_ => {
					if (this._autoLinkShrinkBS != null && this._autoLinkShrinkBS.enabledSelf)
						this._autoLinkShrinkBS.value = !this._autoLinkShrinkBS.value;
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

			// シェーダープリセット
			EnsureShaderPresetUserFolder();
			this._shaderPresetSelect = this.rootVisualElement.Q<DropdownField>("shader-preset-select");
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
				if (this._toolConsoleScroll == null) return;
				var lines = this._toolConsoleScroll.Children()
					.OfType<Label>()
					.Select(l => l.text);
				string text = string.Join("\n", lines);
				text += this.BuildConsoleDiagnosticInfo();
				EditorGUIUtility.systemCopyBuffer = text;
			});

			// リソース初期化ボタン
			var resetBtn = this.rootVisualElement.Q<Button>("reset-resources");
			resetBtn?.RegisterCallback<ClickEvent>(_ =>
			{
				if (EditorUtility.DisplayDialog(
					S("window.reset_resources") ?? "Reset Resources",
					S("window.reset_resources_confirm_body") ?? "This will delete the Resource folder and re-extract essential resources.\n\nContinue?",
					"OK",
					"Cancel"))
				{
					ResourceAutoExtractor.ResetResources(skipConfirmDialog: true);
				}
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
						ToolConsole.Warn("Window", $"finger[{i}]: AdditionalObject GUID not found: {resolvedGuid} (registryName={registryName})");
						continue;
					}
					GameObject? obj = MDNailToolAssetLoader.LoadPrefabSafe(objectPath);
					if (obj == null)
					{
						ToolConsole.Warn("Window", $"finger[{i}]: AdditionalObject could not load: {objectPath} (registryName={registryName})");
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

	}
}

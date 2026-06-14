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
		private static void OnChangeRemoveCurrentNail(ChangeEvent<bool> evt) { GlobalSetting.RemoveCurrentNail = evt.newValue; }
		private static void OnChangeBackup(ChangeEvent<bool> evt) { GlobalSetting.Backup = evt.newValue; }
		private void OnChangeForModularAvatar(ChangeEvent<bool> evt)
		{
			GlobalSetting.UseModularAvatar = evt.newValue;
			this.UpdateMASubOptionsVisibility(evt.newValue);
			this.UpdateBlendShapeVariantDropDown();
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
			if (this._autoLinkShrinkBS != null) {
				bool en = useMA && (this._bakeBlendShapes?.value == true);
				this._autoLinkShrinkBS.SetEnabled(en);
				if (!en && this._autoLinkShrinkBS.value) this._autoLinkShrinkBS.value = false;
			}
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
		private void OnToggleTryout()
		{
			this._tryoutActive = !this._tryoutActive;
			GlobalSetting.EnableSceneWearingPreview = this._tryoutActive;
			this.UpdateTryoutVisual();

			var avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			if (avatar != null)
			{
				this._scenePreviewController ??= new MDNailScenePreviewController(SCENE_PREVIEW_NAME);
				this._scenePreviewController.SetScenePreviewActive(avatar, this._tryoutActive);
			}
			else if (!this._tryoutActive)
			{
				// アバター未選択で試着OFFに切り替えた場合、残存プレビューを念のため掃除
				this.CleanupScenePreview();
			}

			if (this._tryoutActive) this.UpdateScenePreview(immediate: true);
		}

		private void UpdateTryoutVisual()
		{
			if (this._tryoutToggle != null)
			{
				string key = this._tryoutActive ? "window.tryout_toggle_on" : "window.tryout_toggle_off";
				string fallback = this._tryoutActive ? "シーンで試着: ON" : "シーンで試着: OFF";
				this._tryoutToggle.text = S(key) ?? fallback;
				if (this._tryoutActive) this._tryoutToggle.AddToClassList("active");
				else this._tryoutToggle.RemoveFromClassList("active");
			}
			if (this._tryoutBanner != null)
			{
				this._tryoutBanner.style.display = this._tryoutActive ? DisplayStyle.Flex : DisplayStyle.None;
			}
		}

		private void RebuildShaderPresetSelect()
		{
			if (this._shaderPresetSelect == null) return;

			var choices = new List<string> { SHADER_PRESET_NONE_LABEL };
			choices.AddRange(ShaderPresetScanner.ScanAllPresetNames());
			this._shaderPresetSelect.choices = choices;

			string? saved = GlobalSetting.SelectedShaderPreset;
			string toSelect = (string.IsNullOrEmpty(saved) || !choices.Contains(saved!)) ? SHADER_PRESET_NONE_LABEL : saved!;
			this._shaderPresetSelect.SetValueWithoutNotify(toSelect);
		}

		private void OnChangeShaderPresetSelect(ChangeEvent<string> evt)
		{
			string newValue = evt.newValue;
			string? saveValue = (string.IsNullOrEmpty(newValue) || newValue == SHADER_PRESET_NONE_LABEL) ? null : newValue;
			GlobalSetting.SelectedShaderPreset = saveValue;
			INailProcessor.ClearPreviewMaterialCash();
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
		}

		private void OnClickShaderPresetReload()
		{
			AssetDatabase.Refresh();
			ShaderPresetScanner.InvalidateCache();
			INailProcessor.ClearCreatedMaterialCash();
			INailProcessor.ClearPreviewMaterialCash();
			this.RebuildShaderPresetSelect();
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
		}

		private void OnClickShaderPresetPing()
		{
			EnsureShaderPresetUserFolder();
			string folderPath = MDNailToolDefines.SHADER_PRESET_USER_PATH.TrimEnd('/');
			UnityEngine.Object? folder = MDNailToolAssetLoader.LoadAssetSafe<UnityEngine.Object>(folderPath);
			if (folder != null)
			{
				Selection.activeObject = folder;
				EditorGUIUtility.PingObject(folder);
			}
		}

		private void OnClickShaderPresetSettingsToggle()
		{
			if (this._shaderPresetSettingsArea == null) return;
			this._shaderPresetSettingsOpen = !this._shaderPresetSettingsOpen;
			if (this._shaderPresetSettingsOpen)
			{
				this.RebuildShaderPresetSettingsList();
				this._shaderPresetSettingsArea.style.display = DisplayStyle.Flex;
				this._shaderPresetSettingsToggleBtn?.AddToClassList("active");
			}
			else
			{
				this._shaderPresetSettingsArea.style.display = DisplayStyle.None;
				this._shaderPresetSettingsToggleBtn?.RemoveFromClassList("active");
			}
		}

		private void RebuildShaderPresetSettingsList()
		{
			if (this._shaderPresetSettingsList == null) return;
			this._shaderPresetSettingsList.Clear();

			List<string> allNames = ShaderPresetScanner.ScanAllPresetNamesIncludingHidden();
			if (allNames.Count == 0)
			{
				Label empty = new() { text = S("window.shader_preset_no_presets") ?? "(no presets)" };
				empty.style.fontSize = 10;
				empty.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
				this._shaderPresetSettingsList.Add(empty);
				return;
			}

			foreach (string name in allNames)
			{
				bool hidden = ShaderPresetScanner.IsHidden(name);
				VisualElement row = new();
				row.style.flexDirection = FlexDirection.Row;
				row.style.alignItems = Align.Center;
				row.AddToClassList("mdn-shader-preset-settings-row");

				Toggle tg = new() { value = !hidden };
				Label label = new(name);
				label.style.marginLeft = 4;

				string capturedName = name;
				tg.RegisterValueChangedCallback(evt =>
				{
					ShaderPresetScanner.SetHidden(capturedName, !evt.newValue);
					this.RebuildShaderPresetSelect();
				});

				row.Add(tg);
				row.Add(label);
				this._shaderPresetSettingsList.Add(row);
			}
		}

		private void OnClickShaderPresetSave()
		{
			if (this._shaderPresetAddField?.value is Material mat)
			{
				this.HandleShaderPresetDrop(new List<Material> { mat });
				this._shaderPresetAddField.SetValueWithoutNotify(null);
			}
		}

		private void HandleShaderPresetDrop(List<Material> droppedMaterials)
		{
			if (droppedMaterials.Count == 0) return;

			EnsureShaderPresetUserFolder();

			var addedNames = new List<string>();
			foreach (Material mat in droppedMaterials)
			{
				string srcPath = AssetDatabase.GetAssetPath(mat);
				if (string.IsNullOrEmpty(srcPath)) continue;

				string fileName = System.IO.Path.GetFileName(srcPath);
				string dstPath = MDNailToolDefines.SHADER_PRESET_USER_PATH + fileName;
				if (System.IO.File.Exists(dstPath))
				{
					Debug.LogWarning($"[MDNailTool] Shader preset already exists: {fileName}");
					continue;
				}
				AssetDatabase.CopyAsset(srcPath, dstPath);
				AssetDatabase.ImportAsset(dstPath, ImportAssetOptions.ForceSynchronousImport);
				addedNames.Add(System.IO.Path.GetFileNameWithoutExtension(fileName));
			}

			if (addedNames.Count > 0)
			{
				AssetDatabase.Refresh();
				ShaderPresetScanner.InvalidateCache();
				this.RebuildShaderPresetSelect();
				this.RebuildShaderPresetSettingsList();
				string firstAdded = ShaderPresetScanner.USER_PREFIX + addedNames[0];
				this._shaderPresetSelect!.value = firstAdded;
			}
		}

		private static void EnsureShaderPresetUserFolder()
		{
			string folder = MDNailToolDefines.SHADER_PRESET_USER_PATH.TrimEnd('/');
			if (AssetDatabase.IsValidFolder(folder)) return;

			string parent = System.IO.Path.GetDirectoryName(folder)!.Replace('\\', '/');
			string folderName = System.IO.Path.GetFileName(folder);
			if (!AssetDatabase.IsValidFolder(parent))
			{
				System.IO.Directory.CreateDirectory(parent);
				AssetDatabase.Refresh();
			}
			AssetDatabase.CreateFolder(parent, folderName);
		}

	}
}

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
				bool maEnabled = GlobalSetting.UseModularAvatar;
				IEnumerable<AvatarBlendShapeVariant> filtered = maEnabled
					? variants
					: variants.Where(v => !v.Name.StartsWith("Shrink_", StringComparison.OrdinalIgnoreCase));
				choices.AddRange(filtered.Select(v => v.Name));
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
			catch (Exception ex) { sb.AppendLine($"NailTool Version: (取得失敗: {ex.Message})"); }

			try
			{
				string packageJsonPath = "Packages/nadena.dev.modular-avatar/package.json";
				TextAsset? packageJson = MDNailToolAssetLoader.LoadAssetSafe<TextAsset>(packageJsonPath);
				sb.AppendLine($"ModularAvatar: {packageJson?.text switch { string t => Newtonsoft.Json.Linq.JObject.Parse(t)["version"]?.ToString() ?? "unknown", _ => "not installed" }}");
			}
			catch (Exception ex) { sb.AppendLine($"ModularAvatar: (取得失敗: {ex.Message})"); }

			var avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			sb.AppendLine($"Avatar: {avatar?.gameObject?.name ?? "(null)"}");
			sb.AppendLine($"Avatar Root Scale: {avatar?.transform?.localScale.ToString() ?? "(null)"}");
			sb.AppendLine($"AvatarName: {this._avatarDropDowns?.GetAvatarName() ?? "(未設定)"}");
			sb.AppendLine($"Variation: {this._avatarDropDowns?.GetSelectedAvatarVariation()?.VariationName ?? "(null)"}");
			sb.AppendLine($"NailShape: {this._nailShapeDropDown?.value ?? "(null)"}");
			{
				GameObject? diagPrefab = this._avatarDropDowns?.GetSelectedPrefab();
				sb.AppendLine($"NailPrefab: {diagPrefab?.name ?? "(null)"}");
				// GetSelectedPrefab は in-memory orphan を生成するため、 使い終わったら即 destroy する (Scene root 残留防止).
				if (diagPrefab != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(diagPrefab))) {
					Object.DestroyImmediate(diagPrefab);
				}
			}
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
				catch (Exception ex) { ToolConsole.Warn("NailTool", $"BlendShape診断情報の取得に失敗: {ex.Message}"); }
			}

			// ビルド診断（直近のPlayモード/ビルド時の結果、AAOProcessorが収集）
			if (!string.IsNullOrEmpty(AAOProcessor.LastBuildDiagnostic))
			{
				sb.AppendLine("--- Build Diagnostic ---");
				sb.Append(AAOProcessor.LastBuildDiagnostic);
			}
			else
			{
				sb.AppendLine("--- Build Diagnostic ---");
				sb.AppendLine("(no build data — run Play mode first)");
			}

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

			// ネイルデザイン (section index 1) ヘッダ: 並び替え → 検索 の順
			AddHeaderButton("mdn-section-header", 1, S("window.sort") ?? "Sort", null, () => this._nailDesignSelect?.ToggleSortMode());
			AddHeaderButton("mdn-section-header", 1, S("window.search_nail") ?? "Search", "d_Search Icon", () => this._nailDesignSelect?.TriggerSearch());

			// ネイル設定 (section index 2) ヘッダ: デフォルト設定に戻す
			AddHeaderButton("mdn-section-header", 2, S("window.reset_to_default") ?? "Reset", null, ApplyResetNailSettings);

			// 詳細設定Foldoutの開閉状態を記憶する
			const string advancedFoldoutPrefKey = "MDNailTool.AdvancedFoldoutOpen";
			var advancedFoldout = this.rootVisualElement.Q<Foldout>(className: "mdn-advanced-foldout");
			if (advancedFoldout != null)
			{
				advancedFoldout.SetValueWithoutNotify(EditorPrefs.GetBool(advancedFoldoutPrefKey, false));
				advancedFoldout.RegisterValueChangedCallback(evt =>
				{
					EditorPrefs.SetBool(advancedFoldoutPrefKey, evt.newValue);
				});
			}
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

		private Button CreateHeaderButton(string label, string? iconName, System.Action onClick)
		{
			var btn = new Button(onClick);
			btn.style.height = 20;
			btn.style.paddingLeft = 8;
			btn.style.paddingRight = 8;
			btn.style.marginLeft = 4;
			btn.style.flexDirection = FlexDirection.Row;
			btn.style.alignItems = Align.Center;
			btn.style.flexShrink = 0;
			if (!string.IsNullOrEmpty(iconName))
			{
				var icon = new Image { image = EditorGUIUtility.Load(iconName) as Texture2D };
				icon.style.width = 12; icon.style.height = 12; icon.style.marginRight = 3;
				icon.tintColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : Color.black;
				icon.pickingMode = PickingMode.Ignore;
				btn.Add(icon);
			}
			var lbl = new Label(label) {
				style = {
					fontSize = 10,
					whiteSpace = WhiteSpace.NoWrap,
					unityTextAlign = TextAnchor.MiddleCenter,
					paddingTop = 0, paddingBottom = 0, paddingLeft = 0, paddingRight = 0,
				}
			};
			lbl.pickingMode = PickingMode.Ignore;
			btn.Add(lbl);
			return btn;
		}

		private void AddHeaderButton(string headerClass, int headerIndex, string label, string? iconName, System.Action onClick)
		{
			var headers = this.rootVisualElement.Query(className: headerClass).ToList();
			if (headerIndex >= headers.Count) return;
			var header = headers[headerIndex];
			header.style.flexDirection = FlexDirection.Row;
			header.style.alignItems = Align.Center;
			// 右寄せ Spacer (1度だけ追加). 以降のボタンは隣接表示.
			const string SPACER_NAME = "mdn-header-spacer";
			if (header.Q<VisualElement>(SPACER_NAME) == null)
			{
				var spacer = new VisualElement { name = SPACER_NAME };
				spacer.style.flexGrow = 1;
				header.Add(spacer);
			}
			var btn = CreateHeaderButton(label, iconName, onClick);
			header.Add(btn);
		}

		private void ApplyResetNailSettings()
		{
			if (this._tglHandActive != null) this._tglHandActive.value = true;
			if (this._tglFootActive != null) this._tglFootActive.value = true;
			if (this._tglHandDetail != null) this._tglHandDetail.value = false;
			if (this._tglFootDetail != null) this._tglFootDetail.value = false;
			// 指ごと有効化 (bulk 全 ON で 20 指すべて enabled に揃える).
			if (this._bulkLeftHand != null) this._bulkLeftHand.value = true;
			if (this._bulkRightHand != null) this._bulkRightHand.value = true;
			if (this._bulkLeftFoot != null) this._bulkLeftFoot.value = true;
			if (this._bulkRightFoot != null) this._bulkRightFoot.value = true;
			// 追加マテリアル / 追加オブジェクトは現在の選択を維持 (ネイルに設定されてる値を尊重).
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

			// OFF
			if (this._enableDirectMaterial != null) this._enableDirectMaterial.value = false;
			if (this._penetrationCorrection != null) this._penetrationCorrection.value = false;

			// 試着トグルはOFF固定 (毎回OFF運用)
			this._tryoutActive = false;
			GlobalSetting.EnableSceneWearingPreview = false;
			this.UpdateTryoutVisual();
		}

	}
}

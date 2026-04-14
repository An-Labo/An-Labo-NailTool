using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using static world.anlabo.mdnailtool.Editor.Language.LanguageManager;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Window
{
	public class UsageStatsWindow : EditorWindow
	{
		private ScrollView? _scrollView;
		private Label? _statusLabel;

		private const int IMAGE_WIDTH = 1080;
		private const int IMAGE_HEIGHT = 1920;
		private const int SUPERSAMPLE = 2;
		private const string HASHTAG = "#あんらぼぶい #あんらぼ着用ランキング";

		private static readonly Color BG_DARK = new Color(0.08f, 0.08f, 0.14f);
		private static readonly Color BG_DARKER = new Color(0.05f, 0.05f, 0.10f);
		private static readonly Color FALLBACK_ACCENT = new Color(0.73f, 0.84f, 0.87f);
		private static readonly Color GOLD = new Color(1f, 0.84f, 0f);
		private static readonly Color SILVER = new Color(0.82f, 0.82f, 0.82f);
		private static readonly Color BRONZE = new Color(0.9f, 0.6f, 0.3f);

		private static readonly Dictionary<string, Dictionary<string, string>> _imageText = new Dictionary<string, Dictionary<string, string>>
		{
			{ "ja", new Dictionary<string, string> {
				{ "title", "装着回数" }, { "total_wears", "総着用回数" },
				{ "designs", "デザイン" }, { "avatars", "アバター" }, { "times", "回" },
				{ "top_avatars", "使用アバター" },
			}},
			{ "en", new Dictionary<string, string> {
				{ "title", "TOTAL WEARS" }, { "total_wears", "total wears" },
				{ "designs", "designs" }, { "avatars", "avatars" }, { "times", "times" },
				{ "top_avatars", "TOP AVATARS" },
			}},
		};

		private string T(string key, string fallback)
		{
			string lang = Language.LanguageManager.CurrentLanguageData.language;
			if (_imageText.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out string? val))
				return val;
			if (_imageText.TryGetValue("en", out dict) && dict.TryGetValue(key, out val))
				return val;
			return fallback;
		}

		public static void ShowWindow()
		{
			var window = GetWindow<UsageStatsWindow>();
			window.titleContent = new GUIContent(S("usage_stats.title") ?? "Usage Stats");
			window.minSize = new Vector2(550, 600);
			window.Show();
		}

		public void CreateGUI()
		{
			var root = this.rootVisualElement;
			root.style.backgroundColor = new Color(0.18f, 0.18f, 0.22f);
			root.style.paddingTop = 12;
			root.style.paddingRight = 12;
			root.style.paddingBottom = 12;
			root.style.paddingLeft = 12;

			// ヘッダー
			var header = new VisualElement();
			header.style.backgroundColor = new Color(0.14f, 0.14f, 0.18f);
			header.style.borderTopLeftRadius = 8;
			header.style.borderTopRightRadius = 8;
			header.style.borderBottomLeftRadius = 0;
			header.style.borderBottomRightRadius = 0;
			header.style.paddingTop = 16;
			header.style.paddingBottom = 16;
			header.style.paddingLeft = 16;
			header.style.paddingRight = 16;
			header.style.marginBottom = 0;

			var titleLabel = new Label(S("usage_stats.title") ?? "Nail Usage Stats");
			titleLabel.style.fontSize = 18;
			titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
			titleLabel.style.color = Color.white;
			titleLabel.style.marginBottom = 12;
			header.Add(titleLabel);

			// 1行目: ランキング画像を生成
			var generateBtn = CreateStyledButton(S("usage_stats.generate_image") ?? "Generate Image", GenerateAndSaveRankingImage, new Color(0.3f, 0.5f, 0.9f));
			generateBtn.style.alignSelf = Align.FlexStart;
			header.Add(generateBtn);

			// 2行目: Xに共有
			var postXBtn = CreateStyledButton(S("usage_stats.post_x") ?? "Post to X", PostToX, new Color(0.2f, 0.2f, 0.2f));
			postXBtn.style.alignSelf = Align.FlexStart;
			postXBtn.style.marginTop = 4;
			header.Add(postXBtn);

			// 3行目: 説明文（左）+ コピーボタン（右）
			var bottomRow = new VisualElement();
			bottomRow.style.flexDirection = FlexDirection.Row;
			bottomRow.style.justifyContent = Justify.SpaceBetween;
			bottomRow.style.alignItems = Align.Center;
			bottomRow.style.marginTop = 6;

			var hintLabel = new Label(S("usage_stats.post_hint")
				?? "画像をクリップボードにコピーして投稿画面を開きます。Ctrl+V で貼り付けてください。");
			hintLabel.style.color = new Color(0.5f, 0.5f, 0.55f);
			hintLabel.style.fontSize = 10;
			hintLabel.style.flexShrink = 1;
			hintLabel.style.whiteSpace = WhiteSpace.Normal;
			bottomRow.Add(hintLabel);

			var copyBtn = CreateStyledButton(S("usage_stats.copy") ?? "Copy", CopyToClipboard, new Color(0.25f, 0.25f, 0.3f));
			copyBtn.style.flexShrink = 0;
			copyBtn.style.marginRight = 0;
			bottomRow.Add(copyBtn);

			header.Add(bottomRow);

			_statusLabel = new Label("");
			_statusLabel.style.marginTop = 4;
			_statusLabel.style.color = new Color(0.5f, 0.85f, 0.5f);
			_statusLabel.style.fontSize = 11;
			header.Add(_statusLabel);

			root.Add(header);

			_scrollView = new ScrollView(ScrollViewMode.Vertical);
			_scrollView.style.flexGrow = 1;
			_scrollView.style.backgroundColor = new Color(0.16f, 0.16f, 0.20f);
			_scrollView.style.borderBottomLeftRadius = 8;
			_scrollView.style.borderBottomRightRadius = 8;
			_scrollView.style.paddingTop = 8;
			_scrollView.style.paddingBottom = 8;
			_scrollView.style.paddingLeft = 8;
			_scrollView.style.paddingRight = 8;
			root.Add(_scrollView);

			BuildStats();
		}

		private Button CreateStyledButton(string text, Action action, Color bgColor)
		{
			var btn = new Button(action) { text = text };
			btn.style.backgroundColor = bgColor;
			btn.style.color = Color.white;
			btn.style.borderTopLeftRadius = 4;
			btn.style.borderTopRightRadius = 4;
			btn.style.borderBottomLeftRadius = 4;
			btn.style.borderBottomRightRadius = 4;
			btn.style.height = 30;
			btn.style.marginRight = 6;
			btn.style.paddingLeft = 12;
			btn.style.paddingRight = 12;
			btn.style.borderTopWidth = 0;
			btn.style.borderBottomWidth = 0;
			btn.style.borderLeftWidth = 0;
			btn.style.borderRightWidth = 0;
			return btn;
		}

		private void BuildStats()
		{
			if (_scrollView == null) return;
			_scrollView.Clear();

			// ネイルランキング + アバターランキング 横並び
			var topRow = new VisualElement();
			topRow.style.flexDirection = FlexDirection.Row;
			topRow.style.marginBottom = 4;

			var mergedRanking = BuildMergedRanking();
			var nailSection = BuildStyledSection(S("usage_stats.design_ranking") ?? "Nail Ranking", mergedRanking, true);
			nailSection.style.flexGrow = 1;
			nailSection.style.flexBasis = 0;
			nailSection.style.marginRight = 4;
			topRow.Add(nailSection);

			var avatarData = FilterAvatarData(GlobalSetting.AvatarUseCount);
			var avatarSection = BuildStyledSection(S("usage_stats.avatar_ranking") ?? "Avatar Ranking", avatarData, false, true);
			avatarSection.style.flexGrow = 1;
			avatarSection.style.flexBasis = 0;
			avatarSection.style.marginLeft = 4;
			topRow.Add(avatarSection);

			_scrollView.Add(topRow);

			// シェイプ
			_scrollView.Add(BuildStyledSection(S("usage_stats.shape_ranking") ?? "Nail Shape Ranking", GlobalSetting.NailShapeUseCount));

			// オプション
			if (GlobalSetting.OptionUseCount.Count > 0)
				_scrollView.Add(BuildStyledSection(S("usage_stats.option_ranking") ?? "Option Usage", GlobalSetting.OptionUseCount));

			// 追加マテリアル
			var am = GlobalSetting.AdditionalMaterialUseCount;
			if (am.Count > 0) _scrollView.Add(BuildStyledSection(S("usage_stats.additional_material_ranking") ?? "Additional Material", am));

			// 追加オブジェクト
			var ao = GlobalSetting.AdditionalObjectUseCount;
			if (ao.Count > 0) _scrollView.Add(BuildStyledSection(S("usage_stats.additional_object_ranking") ?? "Additional Object", ao));
		}

		#region UI Sections

		private Dictionary<string, int> FilterAvatarData(Dictionary<string, int> data)
		{
			return data.Where(kvp => kvp.Key.Contains("::"))
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
		}

		/// <summary>
		/// デザインとカラバリを同一軸で統合したランキングを作る。
		/// カラバリが1種のデザイン → デザイン名で表示
		/// カラバリが複数のデザイン → カラバリ名で個別表示
		/// </summary>
		private Dictionary<string, int> BuildMergedRanking()
		{
			var designUse = GlobalSetting.DesignUseCount;
			var variationUse = GlobalSetting.VariationUseCount;
			var merged = new Dictionary<string, int>();

			foreach (var d in designUse)
			{
				var variations = variationUse
					.Where(v => v.Key.StartsWith(d.Key + ":"))
					.ToList();

				int variationTotal = variations.Sum(v => v.Value);
				if (variations.Count <= 1 || variationTotal < d.Value / 2)
				{
					// カラバリなし/1つ/記録不十分 → デザイン名で表示
					merged[d.Key] = d.Value;
				}
				else
				{
					// カラバリが十分記録されている → 各カラバリを個別表示
					foreach (var v in variations)
					{
						merged[v.Key] = v.Value;
					}
				}
			}

			return merged;
		}

		private VisualElement BuildStyledSection(string title, Dictionary<string, int> data,
			bool isMergedRanking = false, bool isAvatarSection = false)
		{
			var container = new VisualElement();
			container.style.backgroundColor = new Color(0.14f, 0.14f, 0.18f);
			container.style.borderTopLeftRadius = 6;
			container.style.borderTopRightRadius = 6;
			container.style.borderBottomLeftRadius = 6;
			container.style.borderBottomRightRadius = 6;
			container.style.marginTop = 8;
			container.style.marginBottom = 4;
			container.style.paddingTop = 12;
			container.style.paddingBottom = 8;
			container.style.paddingLeft = 12;
			container.style.paddingRight = 12;

			// セクションヘッダー
			var headerRow = new VisualElement();
			headerRow.style.flexDirection = FlexDirection.Row;
			headerRow.style.justifyContent = Justify.SpaceBetween;
			headerRow.style.marginBottom = 8;

			var titleLabel = new Label(title);
			titleLabel.style.fontSize = 14;
			titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
			titleLabel.style.color = new Color(0.9f, 0.9f, 0.95f);
			headerRow.Add(titleLabel);

			var countLabel = new Label($"{data.Count}");
			countLabel.style.color = new Color(0.5f, 0.5f, 0.6f);
			countLabel.style.fontSize = 12;
			countLabel.style.unityTextAlign = TextAnchor.MiddleRight;
			headerRow.Add(countLabel);

			container.Add(headerRow);

			// 区切り線
			var separator = new VisualElement();
			separator.style.height = 1;
			separator.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
			separator.style.marginBottom = 6;
			container.Add(separator);

			var sorted = data.OrderByDescending(kvp => kvp.Value).ToList();
			int rank = 0;
			foreach (var kvp in sorted)
			{
				rank++;
				var row = new VisualElement();
				row.style.flexDirection = FlexDirection.Row;
				row.style.paddingTop = 4;
				row.style.paddingBottom = 4;
				row.style.paddingLeft = 4;
				row.style.paddingRight = 4;
				row.style.borderTopLeftRadius = 4;
				row.style.borderTopRightRadius = 4;
				row.style.borderBottomLeftRadius = 4;
				row.style.borderBottomRightRadius = 4;

				if (rank <= 3) row.style.backgroundColor = new Color(1f, 0.95f, 0.7f, 0.06f);
				if (rank % 2 == 0 && rank > 3) row.style.backgroundColor = new Color(1f, 1f, 1f, 0.02f);

				// ランク番号
				Color rankColor = rank == 1 ? GOLD : rank == 2 ? SILVER : rank == 3 ? BRONZE : new Color(0.5f, 0.5f, 0.55f);
				var rl = new Label($"#{rank}");
				rl.style.width = 36;
				rl.style.unityTextAlign = TextAnchor.MiddleRight;
				rl.style.marginRight = 10;
				rl.style.color = rankColor;
				rl.style.fontSize = rank <= 3 ? 13 : 12;
				if (rank <= 3) rl.style.unityFontStyleAndWeight = FontStyle.Bold;
				row.Add(rl);

				// 名前
				string displayName;
				if (isAvatarSection)
				{
					string[] parts = kvp.Key.Split(new[] { "::" }, StringSplitOptions.None);
					displayName = parts.Length == 2
						? GetAvatarDisplayName(parts[0], parts[1])
						: kvp.Key;
				}
				else if (isMergedRanking)
				{
					displayName = kvp.Key.Contains(":")
						? FormatVariationName(kvp.Key)
						: GetDesignDisplayName(kvp.Key);
				}
				else
				{
					displayName = FormatDisplayName(kvp.Key);
				}

				var nl = new Label(displayName);
				nl.style.flexGrow = 1;
				nl.style.overflow = Overflow.Hidden;
				nl.style.color = rank <= 3 ? new Color(0.95f, 0.95f, 0.97f) : new Color(0.75f, 0.75f, 0.8f);
				nl.style.fontSize = 12;
				row.Add(nl);

				// カウント
				var cl = new Label($"{kvp.Value}");
				cl.style.width = 50;
				cl.style.unityTextAlign = TextAnchor.MiddleRight;
				cl.style.color = rank <= 3 ? new Color(0.9f, 0.85f, 0.7f) : new Color(0.5f, 0.5f, 0.55f);
				cl.style.fontSize = 12;
				row.Add(cl);

				container.Add(row);
			}

			if (sorted.Count == 0)
			{
				var el = new Label(S("usage_stats.no_data") ?? "No data");
				el.style.color = new Color(0.4f, 0.4f, 0.45f);
				el.style.fontSize = 12;
				el.style.paddingTop = 8;
				el.style.paddingBottom = 8;
				container.Add(el);
			}

			return container;
		}

		private string FormatDisplayName(string key)
		{
			if (key.Contains("::"))
			{
				string[] parts = key.Split(new[] { "::" }, StringSplitOptions.None);
				if (parts.Length == 2 && !string.IsNullOrEmpty(parts[0]))
					return GetAvatarDisplayName(parts[0], parts[1]);
			}
			if (key.Contains(":") && !key.Contains("::"))
				return FormatVariationName(key);
			return key;
		}

		private string FormatVariationName(string key)
		{
			string[] parts = key.Split(':');
			if (parts.Length >= 3)
			{
				string designDisplay = GetDesignDisplayName(parts[0]);
				return $"{designDisplay} / {parts[2]}";
			}
			if (parts.Length >= 2)
				return $"{parts[0]} / {parts[1]}";
			return key;
		}

		private string ExtractDesignName(string variationKey)
		{
			string[] parts = variationKey.Split(':');
			return parts.Length >= 1 ? parts[0] : variationKey;
		}

		#endregion

		#region Ranking Image Generation

		private struct RankEntry
		{
			public string DisplayName;
			public string DesignKey;
			public int Count;
		}

		private List<RankEntry> BuildMergedRankingForImage()
		{
			var designUse = GlobalSetting.DesignUseCount;
			var variationUse = GlobalSetting.VariationUseCount;
			var entries = new List<RankEntry>();

			foreach (var d in designUse)
			{
				var variations = variationUse
					.Where(v => v.Key.StartsWith(d.Key + ":"))
					.ToList();

				if (variations.Count <= 1)
				{
					entries.Add(new RankEntry
					{
						DisplayName = GetDesignDisplayName(d.Key),
						DesignKey = d.Key,
						Count = d.Value
					});
				}
				else
				{
					foreach (var v in variations)
					{
						entries.Add(new RankEntry
						{
							DisplayName = FormatVariationName(v.Key),
							DesignKey = d.Key,
							Count = v.Value
						});
					}
				}
			}

			entries.Sort((a, b) => b.Count.CompareTo(a.Count));
			return entries;
		}

		private Texture2D GenerateRankingImage()
		{
			int w = IMAGE_WIDTH * SUPERSAMPLE;
			int h = IMAGE_HEIGHT * SUPERSAMPLE;
			float s = SUPERSAMPLE;

			// === データ取得（RT設定前に完了させる）===
			var designRanking = GlobalSetting.DesignUseCount
				.OrderByDescending(kvp => kvp.Value).ToList();
			var avatarRanking = FilterAvatarData(GlobalSetting.AvatarUseCount)
				.OrderByDescending(kvp => kvp.Value).ToList();
			int totalCount = designRanking.Sum(kvp => kvp.Value);
			int designCount = designRanking.Count;

			// アクセントカラー + 背景色（RT操作を含むので先に取得）
			Color accent = FALLBACK_ACCENT;
			Color[] bgColors = { BG_DARK, BG_DARK, BG_DARKER };
			Texture2D? heroThumb = null;
			string heroName = "";

			if (designRanking.Count > 0)
			{
				var top = designRanking[0];
				heroName = GetDesignDisplayName(top.Key);
				heroThumb = GetDesignThumbnail(top.Key);

				Color[]? colors = GetDominantColorsForDesign(top.Key);
				if (colors != null && colors.Length >= 1)
				{
					accent = colors[0];
					bgColors = new Color[3];
					bgColors[0] = colors.Length >= 1 ? colors[0] : accent;
					bgColors[1] = colors.Length >= 2 ? colors[1] : accent;
					bgColors[2] = colors.Length >= 3 ? colors[2] : accent;
				}
			}

			// === GL描画開始 ===
			var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
			var prevRT = RenderTexture.active;
			RenderTexture.active = rt;

			GL.Clear(true, true, Color.black);
			GL.PushMatrix();
			try
			{
			GL.LoadPixelMatrix(0, w, h, 0);

			// === 背景（1位のネイルカラー3色グラデーション）===
			Draw3ColorGradient(w, h, bgColors[0], bgColors[1], bgColors[2]);

			float cx = w / 2f;
			float margin = 80 * s;
			float contentW = w - margin * 2;

			float sr = 14 * s; // シャドウ半径

			// === 総着用回数（小文字ラベル）===
			DrawTextCenteredS(T("total_wears", "total wears"), cx, 70 * s,
				(int)(16 * s), Color.white, FontStyle.Normal, sr);

			// === 回数・デザイン・アバター（横並び）===
			string statsLine = $"{totalCount}{T("times", "times")}  ·  {designCount} {T("designs", "designs")}  ·  {avatarRanking.Count} {T("avatars", "avatars")}";
			DrawTextCenteredS(statsLine, cx, 110 * s,
				(int)(16 * s), Color.white, FontStyle.Normal, sr);

			// === 1位ヒーロー ===
			if (designRanking.Count > 0)
			{
				var top = designRanking[0];
				float thumbSize = 400 * s;
				float thumbX = (w - thumbSize) / 2f;
				float thumbY = 240 * s;

				DrawGlow(cx, thumbY + thumbSize / 2, thumbSize * 0.9f, accent);

				if (heroThumb != null)
				{
					// サムネイルの影（楕円グロー）
					float shadowCx = thumbX + thumbSize / 2f;
					float shadowCy = thumbY + thumbSize / 2f + 20 * s;
					DrawGlow(shadowCx, shadowCy, thumbSize * 0.7f, new Color(0, 0, 0, 0.6f));
					DrawTexture(heroThumb, thumbX, thumbY, thumbSize, thumbSize);
				}

				float infoY = thumbY + thumbSize + 32 * s;

				DrawTextCenteredS(heroName, cx, infoY, (int)(36 * s),
					Color.white, FontStyle.Bold, sr);
				infoY += 50 * s;

				DrawTextCenteredS($"{top.Value} {T("times", "times")}", cx, infoY,
					(int)(28 * s), Color.white, FontStyle.Normal, sr);
			}

			// === 2-10位 ===
			float listY = 970 * s;

			int rankCount = Mathf.Min(designRanking.Count, 10);
			for (int i = 1; i < rankCount; i++)
			{
				var kvp = designRanking[i];
				string name = GetDesignDisplayName(kvp.Key);

				Color rankColor = i == 1 ? SILVER : i == 2 ? BRONZE : Color.white;
				Color textColor = Color.white;
				int fontSize = i < 3 ? (int)(24 * s) : (int)(22 * s);

				DrawTextS($"#{i + 1}", margin, listY, fontSize, rankColor, FontStyle.Bold, sr);
				DrawTextS(name, margin + 64 * s, listY, fontSize, textColor, FontStyle.Normal, sr);
				DrawTextRightAlignedS($"{kvp.Value}", margin + contentW, listY, fontSize,
					Color.white, FontStyle.Normal, sr);

				listY += (i < 3 ? 46 : 40) * s;
			}

			// === アバターランキング ===
			if (avatarRanking.Count > 0)
			{
				listY += 36 * s;

				DrawTextS(T("top_avatars", "TOP AVATARS"), margin, listY,
					(int)(20 * s), Color.white, FontStyle.Bold, sr);
				listY += 40 * s;

				int avatarCount = Mathf.Min(avatarRanking.Count, 5);
				for (int i = 0; i < avatarCount; i++)
				{
					var kvp = avatarRanking[i];
					string[] parts = kvp.Key.Split(new[] { "::" }, StringSplitOptions.None);
					string dn = parts.Length == 2
						? GetAvatarDisplayName(parts[0], parts[1])
						: kvp.Key;

					Color rc = i == 0 ? GOLD : i == 1 ? SILVER : i == 2 ? BRONZE : Color.white;

					DrawTextS($"#{i + 1}", margin, listY, (int)(20 * s), rc, FontStyle.Bold, sr);
					DrawTextS(dn, margin + 56 * s, listY, (int)(20 * s),
						Color.white, FontStyle.Normal, sr);
					DrawTextRightAlignedS($"{kvp.Value}", margin + contentW, listY, (int)(20 * s),
						Color.white, FontStyle.Normal, sr);
					listY += 36 * s;
				}
			}

			// === フッター ===
			float footerY = h - 100 * s;

			DrawTextCenteredS("An-Labo Nail Tool", cx, footerY, (int)(18 * s),
				Color.white, FontStyle.Bold, sr);

			DrawTextCenteredS(HASHTAG, cx, footerY + 32 * s, (int)(16 * s),
				Color.white, FontStyle.Normal, sr);

			DrawTextCenteredS(DateTime.Now.ToString("yyyy.MM.dd"), cx, footerY + 60 * s,
				(int)(14 * s), Color.white, FontStyle.Normal, sr);

			}
			finally
			{
				GL.PopMatrix();
			}

			Texture2D? tex = null;
			try
			{
				tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
				tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
				tex.Apply();
			}
			finally
			{
				RenderTexture.active = prevRT;
				RenderTexture.ReleaseTemporary(rt);
			}

			var finalTex = DownSample(tex, IMAGE_WIDTH, IMAGE_HEIGHT);
			DestroyImmediate(tex);

			return finalTex;
		}

		private Color[]? GetDominantColorsForDesign(string designName)
		{
			try
			{
				// まずDBのdominantColorsを試す
				using DBNailDesign db = new DBNailDesign();
				var design = db.collection.FirstOrDefault(d => d.DesignName == designName);
				if (design == null) return null;

				if (design.DominantColors != null && design.DominantColors.Length >= 1)
				{
					// フォールバック色(#bad7de)でなければDBの値を使う
					bool isFallback = design.DominantColors.All(dc => dc.Hex == "#bad7de");
					if (!isFallback)
					{
						return design.DominantColors
							.Take(3)
							.Select(dc =>
							{
								ColorUtility.TryParseHtmlString(dc.Hex, out Color c);
								return c;
							}).ToArray();
					}
				}

				// DBにない or フォールバック色ならサムネイルから色を抽出
				Texture2D? thumb = GetDesignThumbnail(designName);
				if (thumb != null)
					return ExtractColorsFromTexture(thumb);
			}
			catch { }

			return null;
		}

		private Color[]? ExtractColorsFromTexture(Texture2D tex)
		{
			try
			{
				var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
				var rt = RenderTexture.GetTemporary(tex.width, tex.height);
				Graphics.Blit(tex, rt);
				var prevRT = RenderTexture.active;
				RenderTexture.active = rt;
				readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
				readable.Apply();
				RenderTexture.active = prevRT;
				RenderTexture.ReleaseTemporary(rt);

				Color32[] pixels = readable.GetPixels32();
				DestroyImmediate(readable);

				// 中央付近のピクセルから平均色を取る
				int cx = tex.width / 2;
				int cy = tex.height / 2;
				int range = Mathf.Min(tex.width, tex.height) / 4;
				float r = 0, g = 0, b = 0;
				int count = 0;

				for (int y = cy - range; y < cy + range; y += 2)
				{
					for (int x = cx - range; x < cx + range; x += 2)
					{
						if (x < 0 || x >= tex.width || y < 0 || y >= tex.height) continue;
						var c = pixels[y * tex.width + x];
						if (c.a < 128) continue;
						r += c.r; g += c.g; b += c.b;
						count++;
					}
				}

				if (count == 0) return null;
				return new[] { new Color(r / count / 255f, g / count / 255f, b / count / 255f) };
			}
			catch { return null; }
		}

		#endregion

		#region Drawing Helpers

		private Material? _glMaterial;
		private Material? _texMaterial;
		private Dictionary<int, Font> _fontCache = new Dictionary<int, Font>();

		private Material GetGLMaterial()
		{
			if (_glMaterial == null)
			{
				var shader = Shader.Find("Hidden/Internal-Colored");
				_glMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
				_glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
				_glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
				_glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
				_glMaterial.SetInt("_ZWrite", 0);
			}
			return _glMaterial;
		}

		private Material GetTexMaterial()
		{
			if (_texMaterial == null)
			{
				var shader = Shader.Find("Unlit/Transparent");
				if (shader == null) shader = Shader.Find("UI/Default");
				_texMaterial = new Material(shader!) { hideFlags = HideFlags.HideAndDontSave };
			}
			return _texMaterial;
		}

		private Font GetJapaneseFont(int fontSize)
		{
			if (_fontCache.TryGetValue(fontSize, out Font? cached) && cached != null)
				return cached;

			string[] fontNames = { "Yu Gothic UI", "Hiragino Sans", "Meiryo", "Arial" };
			foreach (var fontName in fontNames)
			{
				var font = Font.CreateDynamicFontFromOSFont(fontName, fontSize);
				if (font != null)
				{
					_fontCache[fontSize] = font;
					return font;
				}
			}
			var fallback = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
			_fontCache[fontSize] = fallback;
			return fallback;
		}

		private void DrawDarkBackground(int w, int h)
		{
			DrawGradientBackground(w, h, BG_DARK, BG_DARKER);
		}

		private void DrawGradientBackground(int w, int h, Color top, Color bottom)
		{
			GetGLMaterial().SetPass(0);
			GL.Begin(GL.QUADS);
			GL.Color(top);    GL.Vertex3(0, 0, 0);
			GL.Color(top);    GL.Vertex3(w, 0, 0);
			GL.Color(bottom); GL.Vertex3(w, h, 0);
			GL.Color(bottom); GL.Vertex3(0, h, 0);
			GL.End();
		}

		private void Draw3ColorGradient(int w, int h, Color c0, Color c1, Color c2)
		{
			int mid = h / 2;
			GetGLMaterial().SetPass(0);
			GL.Begin(GL.QUADS);
			GL.Color(c0); GL.Vertex3(0, 0, 0);
			GL.Color(c0); GL.Vertex3(w, 0, 0);
			GL.Color(c1); GL.Vertex3(w, mid, 0);
			GL.Color(c1); GL.Vertex3(0, mid, 0);
			GL.End();

			GL.Begin(GL.QUADS);
			GL.Color(c1); GL.Vertex3(0, mid, 0);
			GL.Color(c1); GL.Vertex3(w, mid, 0);
			GL.Color(c2); GL.Vertex3(w, h, 0);
			GL.Color(c2); GL.Vertex3(0, h, 0);
			GL.End();
		}

		private Color WithAlpha(Color c, float alpha)
		{
			return new Color(c.r, c.g, c.b, alpha);
		}

		private void DrawBlurredShadow(string text, float x, float y, int fontSize, FontStyle style, float radius)
		{
			int rings = 12;
			int segments = 16;
			for (int r = 1; r <= rings; r++)
			{
				float t = (float)r / rings;
				float rad = radius * t;
				float alpha = 0.025f * (1f - t * 0.8f);
				Color sc = new Color(0, 0, 0, alpha);
				for (int seg = 0; seg < segments; seg++)
				{
					float angle = Mathf.Deg2Rad * (360f * seg / segments);
					DrawText(text, x + Mathf.Cos(angle) * rad, y + Mathf.Sin(angle) * rad, fontSize, sc, style);
				}
			}
		}

		private void DrawTextS(string text, float x, float y, int fontSize, Color color, FontStyle style, float shadowRadius = 6f)
		{
			DrawBlurredShadow(text, x, y, fontSize, style, shadowRadius);
			DrawText(text, x, y, fontSize, color, style);
		}

		private void DrawTextCenteredS(string text, float cx, float y, int fontSize, Color color, FontStyle style, float shadowRadius = 6f)
		{
			if (string.IsNullOrEmpty(text)) return;
			var font = GetJapaneseFont(fontSize);
			font.RequestCharactersInTexture(text, fontSize, style);
			float totalWidth = 0;
			foreach (char ch in text)
				if (font.GetCharacterInfo(ch, out CharacterInfo ci, fontSize, style))
					totalWidth += ci.advance;
			float startX = cx - totalWidth / 2f;
			DrawBlurredShadow(text, startX, y, fontSize, style, shadowRadius);
			DrawText(text, startX, y, fontSize, color, style);
		}

		private void DrawTextRightAlignedS(string text, float rightX, float y, int fontSize, Color color, FontStyle style, float shadowRadius = 6f)
		{
			if (string.IsNullOrEmpty(text)) return;
			var font = GetJapaneseFont(fontSize);
			font.RequestCharactersInTexture(text, fontSize, style);
			float totalWidth = 0;
			foreach (char ch in text)
				if (font.GetCharacterInfo(ch, out CharacterInfo ci, fontSize, style))
					totalWidth += ci.advance;
			float startX = rightX - totalWidth;
			DrawBlurredShadow(text, startX, y, fontSize, style, shadowRadius);
			DrawText(text, startX, y, fontSize, color, style);
		}

		private void DrawGlow(float cx, float cy, float radius, Color color)
		{
			int rings = 16;
			int segments = 32;
			GetGLMaterial().SetPass(0);

			for (int ring = rings - 1; ring >= 0; ring--)
			{
				float t = (float)ring / rings;
				float r = radius * (0.2f + t * 0.8f);
				float alpha = (1f - t) * 0.15f;
				Color c = new Color(color.r, color.g, color.b, alpha);

				GL.Begin(GL.TRIANGLES);
				for (int seg = 0; seg < segments; seg++)
				{
					float a0 = Mathf.Deg2Rad * (360f * seg / segments);
					float a1 = Mathf.Deg2Rad * (360f * (seg + 1) / segments);

					GL.Color(c);
					GL.Vertex3(cx, cy, 0);
					GL.Vertex3(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r, 0);
					GL.Vertex3(cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r, 0);
				}
				GL.End();
			}
		}

		private void DrawRect(float x, float y, float w, float h, Color color)
		{
			GetGLMaterial().SetPass(0);
			GL.Begin(GL.QUADS);
			GL.Color(color);
			GL.Vertex3(x, y, 0);
			GL.Vertex3(x + w, y, 0);
			GL.Vertex3(x + w, y + h, 0);
			GL.Vertex3(x, y + h, 0);
			GL.End();
		}

		private void DrawLine(float x0, float y0, float x1, float y1, Color color, float thickness)
		{
			float half = thickness / 2;
			GetGLMaterial().SetPass(0);
			GL.Begin(GL.QUADS);
			GL.Color(color);
			GL.Vertex3(x0, y0 - half, 0);
			GL.Vertex3(x1, y1 - half, 0);
			GL.Vertex3(x1, y1 + half, 0);
			GL.Vertex3(x0, y0 + half, 0);
			GL.End();
		}

		private void DrawText(string text, float x, float y, int fontSize, Color color, FontStyle style)
		{
			if (string.IsNullOrEmpty(text)) return;

			var font = GetJapaneseFont(fontSize);
			font.RequestCharactersInTexture(text, fontSize, style);

			font.material.SetPass(0);
			GL.Begin(GL.QUADS);

			float cursorX = x;
			foreach (char ch in text)
			{
				if (!font.GetCharacterInfo(ch, out CharacterInfo ci, fontSize, style))
					continue;

				GL.Color(color);

				float vx0 = cursorX + ci.minX;
				float vx1 = cursorX + ci.maxX;
				float vy0 = y - ci.maxY;
				float vy1 = y - ci.minY;

				GL.TexCoord(ci.uvTopLeft);
				GL.Vertex3(vx0, vy0, 0);
				GL.TexCoord(ci.uvTopRight);
				GL.Vertex3(vx1, vy0, 0);
				GL.TexCoord(ci.uvBottomRight);
				GL.Vertex3(vx1, vy1, 0);
				GL.TexCoord(ci.uvBottomLeft);
				GL.Vertex3(vx0, vy1, 0);

				cursorX += ci.advance;
			}

			GL.End();
		}

		private void DrawTextCentered(string text, float cx, float y, int fontSize, Color color, FontStyle style)
		{
			if (string.IsNullOrEmpty(text)) return;

			var font = GetJapaneseFont(fontSize);
			font.RequestCharactersInTexture(text, fontSize, style);

			// 幅計算とGL描画を同一Request内で実行（テクスチャ再構築を防ぐ）
			float totalWidth = 0;
			foreach (char ch in text)
			{
				if (font.GetCharacterInfo(ch, out CharacterInfo ci, fontSize, style))
					totalWidth += ci.advance;
			}

			float startX = cx - totalWidth / 2f;
			font.material.SetPass(0);
			GL.Begin(GL.QUADS);
			float cursorX = startX;
			foreach (char ch in text)
			{
				if (!font.GetCharacterInfo(ch, out CharacterInfo ci, fontSize, style))
					continue;
				GL.Color(color);
				GL.TexCoord(ci.uvTopLeft);    GL.Vertex3(cursorX + ci.minX, y - ci.maxY, 0);
				GL.TexCoord(ci.uvTopRight);   GL.Vertex3(cursorX + ci.maxX, y - ci.maxY, 0);
				GL.TexCoord(ci.uvBottomRight); GL.Vertex3(cursorX + ci.maxX, y - ci.minY, 0);
				GL.TexCoord(ci.uvBottomLeft);  GL.Vertex3(cursorX + ci.minX, y - ci.minY, 0);
				cursorX += ci.advance;
			}
			GL.End();
		}

		private void DrawTextRightAligned(string text, float rightX, float y, int fontSize, Color color, FontStyle style)
		{
			if (string.IsNullOrEmpty(text)) return;

			var font = GetJapaneseFont(fontSize);
			font.RequestCharactersInTexture(text, fontSize, style);

			float totalWidth = 0;
			foreach (char ch in text)
			{
				if (font.GetCharacterInfo(ch, out CharacterInfo ci, fontSize, style))
					totalWidth += ci.advance;
			}

			float startX = rightX - totalWidth;
			font.material.SetPass(0);
			GL.Begin(GL.QUADS);
			float cursorX = startX;
			foreach (char ch in text)
			{
				if (!font.GetCharacterInfo(ch, out CharacterInfo ci, fontSize, style))
					continue;
				GL.Color(color);
				GL.TexCoord(ci.uvTopLeft);    GL.Vertex3(cursorX + ci.minX, y - ci.maxY, 0);
				GL.TexCoord(ci.uvTopRight);   GL.Vertex3(cursorX + ci.maxX, y - ci.maxY, 0);
				GL.TexCoord(ci.uvBottomRight); GL.Vertex3(cursorX + ci.maxX, y - ci.minY, 0);
				GL.TexCoord(ci.uvBottomLeft);  GL.Vertex3(cursorX + ci.minX, y - ci.minY, 0);
				cursorX += ci.advance;
			}
			GL.End();
		}

		private void DrawTexture(Texture tex, float x, float y, float width, float height)
		{
			var mat = GetTexMaterial();
			mat.mainTexture = tex;
			mat.SetPass(0);

			GL.Begin(GL.QUADS);
			GL.Color(Color.white);
			GL.TexCoord2(0, 1); GL.Vertex3(x, y, 0);
			GL.TexCoord2(1, 1); GL.Vertex3(x + width, y, 0);
			GL.TexCoord2(1, 0); GL.Vertex3(x + width, y + height, 0);
			GL.TexCoord2(0, 0); GL.Vertex3(x, y + height, 0);
			GL.End();
		}

		private Texture2D DownSample(Texture2D source, int targetWidth, int targetHeight)
		{
			var rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
			rt.filterMode = FilterMode.Bilinear;
			Graphics.Blit(source, rt);

			var prevRT = RenderTexture.active;
			RenderTexture.active = rt;

			var result = new Texture2D(targetWidth, targetHeight, TextureFormat.ARGB32, false);
			result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
			result.Apply();

			RenderTexture.active = prevRT;
			RenderTexture.ReleaseTemporary(rt);

			return result;
		}

		#endregion

		#region Data Helpers

		private Texture2D? GetDesignThumbnail(string designName)
		{
			try
			{
				using DBNailDesign db = new DBNailDesign();
				var design = db.collection.FirstOrDefault(d => d.DesignName == designName);
				if (design == null) return null;

				return MDNailToolAssetLoader.LoadThumbnail(design.ThumbnailGUID, design.DesignName);
			}
			catch { return null; }
		}

		private string GetDesignDisplayName(string designName)
		{
			try
			{
				using DBNailDesign db = new DBNailDesign();
				var design = db.collection.FirstOrDefault(d => d.DesignName == designName);
				if (design?.DisplayNames != null)
				{
					string lang = Language.LanguageManager.CurrentLanguageData.language;
					if (design.DisplayNames.TryGetValue(lang, out string? dn) && !string.IsNullOrEmpty(dn))
						return dn;
					if (design.DisplayNames.TryGetValue("ja", out dn) && !string.IsNullOrEmpty(dn))
						return dn;
					var first = design.DisplayNames.Values.FirstOrDefault();
					if (!string.IsNullOrEmpty(first)) return first!;
				}
			}
			catch { }
			return designName;
		}

		private string GetAvatarDisplayName(string shopName, string avatarName)
		{
			try
			{
				using DBShop db = new DBShop();
				Shop? shop = db.FindShopByName(shopName);
				if (shop != null)
				{
					string lang = Language.LanguageManager.CurrentLanguageData.language;

					string avatarDn = avatarName;
					Entity.Avatar? avatar = shop.FindAvatarByName(avatarName);
					if (avatar?.DisplayNames != null)
					{
						if (avatar.DisplayNames.TryGetValue(lang, out string? adn) && !string.IsNullOrEmpty(adn))
							avatarDn = adn;
						else if (avatar.DisplayNames.TryGetValue("ja", out adn) && !string.IsNullOrEmpty(adn))
							avatarDn = adn;
					}

					string shopDn = shopName;
					if (shop.DisplayNames != null)
					{
						if (shop.DisplayNames.TryGetValue(lang, out string? sdn) && !string.IsNullOrEmpty(sdn))
							shopDn = sdn;
						else if (shop.DisplayNames.TryGetValue("ja", out sdn) && !string.IsNullOrEmpty(sdn))
							shopDn = sdn;
					}

					return $"{avatarDn} ({shopDn})";
				}
			}
			catch { }
			return $"{avatarName} ({shopName})";
		}

		#endregion

		#region Actions

		private string GetSavePath()
		{
			string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
			string dir = Path.Combine(pictures, "An-Labo NailTool");
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			return Path.Combine(dir, $"NailRanking_{DateTime.Now:yyyyMMdd_HHmmss}.png");
		}

		private string? _lastSavedPath;

		private void GenerateAndSaveRankingImage()
		{
			try
			{
				var tex = GenerateRankingImage();
				byte[] png = tex.EncodeToPNG();
				DestroyImmediate(tex);

				_lastSavedPath = GetSavePath();
				File.WriteAllBytes(_lastSavedPath, png);

				SetStatus($"Saved: {_lastSavedPath}");
				Debug.Log($"[MDNailTool] Ranking image saved: {_lastSavedPath}");

				EditorUtility.RevealInFinder(_lastSavedPath);
			}
			catch (Exception e)
			{
				SetStatus($"Error: {e.Message}");
				Debug.LogError($"[MDNailTool] Ranking image generation failed: {e}");
			}
		}

		private void PostToX()
		{
			if (string.IsNullOrEmpty(_lastSavedPath) || !File.Exists(_lastSavedPath))
				GenerateAndSaveRankingImage();

			if (string.IsNullOrEmpty(_lastSavedPath)) return;

			var designRanking = GlobalSetting.DesignUseCount
				.OrderByDescending(kvp => kvp.Value).ToList();
			int totalCount = designRanking.Sum(kvp => kvp.Value);

			string topName = designRanking.Count > 0 ? GetDesignDisplayName(designRanking[0].Key) : "";
			int topCount = designRanking.Count > 0 ? designRanking[0].Value : 0;

			string tweetText = $"My Nail Ranking!\n1st: {topName} ({topCount} {T("times", "times")})\nTotal: {totalCount} {T("times", "times")}\n{HASHTAG}";

			CopyImageToClipboard(_lastSavedPath!);

			string encodedText = Uri.EscapeDataString(tweetText);
			Application.OpenURL($"https://x.com/intent/tweet?text={encodedText}");
		}

		[DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
		[DllImport("user32.dll")] private static extern bool EmptyClipboard();
		[DllImport("user32.dll")] private static extern bool CloseClipboard();
		[DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
		[DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
		[DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
		[DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr hMem);
		[DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr hMem);

		private const uint CF_DIB = 8;
		private const uint GMEM_MOVEABLE = 0x0002;

		private void CopyImageToClipboard(string imagePath)
		{
			try
			{
				byte[] pngBytes = File.ReadAllBytes(imagePath);
				var tex = new Texture2D(2, 2);
				tex.LoadImage(pngBytes);

				int w = tex.width;
				int h = tex.height;
				Color32[] pixels = tex.GetPixels32();
				DestroyImmediate(tex);

				// BITMAPINFOHEADER (40 bytes) + pixel data (BGRA, bottom-up)
				int headerSize = 40;
				int rowBytes = w * 4;
				int dataSize = headerSize + rowBytes * h;

				byte[] dibData = new byte[dataSize];

				// BITMAPINFOHEADER
				BitConverter.GetBytes(headerSize).CopyTo(dibData, 0);   // biSize
				BitConverter.GetBytes(w).CopyTo(dibData, 4);            // biWidth
				BitConverter.GetBytes(h).CopyTo(dibData, 8);            // biHeight (positive = bottom-up)
				BitConverter.GetBytes((short)1).CopyTo(dibData, 12);    // biPlanes
				BitConverter.GetBytes((short)32).CopyTo(dibData, 14);   // biBitCount
				// biCompression = 0 (BI_RGB), rest = 0

				// Pixel data (Unity is top-down, DIB is bottom-up, Unity=RGBA → DIB=BGRA)
				for (int y = 0; y < h; y++)
				{
					int srcRow = y;
					for (int x = 0; x < w; x++)
					{
						var c = pixels[srcRow * w + x];
						int offset = headerSize + (y * w + x) * 4;
						dibData[offset + 0] = c.b;
						dibData[offset + 1] = c.g;
						dibData[offset + 2] = c.r;
						dibData[offset + 3] = c.a;
					}
				}

				IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)dibData.Length);
				if (hMem == IntPtr.Zero)
					throw new OutOfMemoryException("GlobalAlloc failed");

				IntPtr ptr = GlobalLock(hMem);
				if (ptr == IntPtr.Zero)
				{
					GlobalFree(hMem);
					throw new InvalidOperationException("GlobalLock failed");
				}

				Marshal.Copy(dibData, 0, ptr, dibData.Length);
				GlobalUnlock(hMem);

				if (OpenClipboard(IntPtr.Zero))
				{
					EmptyClipboard();
					SetClipboardData(CF_DIB, hMem);
					CloseClipboard();
				}
				else
				{
					GlobalFree(hMem);
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[MDNailTool] Clipboard image copy failed: {e.Message}");
				GUIUtility.systemCopyBuffer = imagePath;
			}
		}

		private void CopyToClipboard()
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("=== Nail Usage Stats ===");
			sb.AppendLine();

			void AppendRanking(string title, Dictionary<string, int> data, Func<string, string>? formatter = null)
			{
				if (data.Count == 0) return;
				sb.AppendLine($"[{title}]");
				var sorted = data.OrderByDescending(kvp => kvp.Value).ToList();
				for (int i = 0; i < sorted.Count; i++)
				{
					string name = formatter != null ? formatter(sorted[i].Key) : sorted[i].Key;
					sb.AppendLine($"#{i + 1} {name}: {sorted[i].Value}");
				}
				sb.AppendLine();
			}

			// デザイン
			AppendRanking(S("usage_stats.design_ranking") ?? "Design Ranking",
				GlobalSetting.DesignUseCount, k => GetDesignDisplayName(k));

			// アバター
			var avatarData = FilterAvatarData(GlobalSetting.AvatarUseCount);
			AppendRanking(S("usage_stats.avatar_ranking") ?? "Avatar Ranking",
				avatarData, k => {
					string[] parts = k.Split(new[] { "::" }, StringSplitOptions.None);
					return parts.Length == 2 ? GetAvatarDisplayName(parts[0], parts[1]) : k;
				});

			// シェイプ
			AppendRanking(S("usage_stats.shape_ranking") ?? "Nail Shape Ranking",
				GlobalSetting.NailShapeUseCount);

			// バリエーション
			AppendRanking(S("usage_stats.variation_ranking") ?? "Variation Ranking",
				GlobalSetting.VariationUseCount, k => FormatVariationName(k));

			// オプション
			AppendRanking(S("usage_stats.option_ranking") ?? "Option Usage",
				GlobalSetting.OptionUseCount);

			// 追加マテリアル
			AppendRanking(S("usage_stats.additional_material_ranking") ?? "Additional Material",
				GlobalSetting.AdditionalMaterialUseCount);

			// 追加オブジェクト
			AppendRanking(S("usage_stats.additional_object_ranking") ?? "Additional Object",
				GlobalSetting.AdditionalObjectUseCount);

			sb.AppendLine($"Total: {GlobalSetting.DesignUseCount.Values.Sum()} {T("times", "times")}");

			GUIUtility.systemCopyBuffer = sb.ToString();
			SetStatus("Copied to clipboard!");
		}

		private void OnDestroy()
		{
			foreach (var font in _fontCache.Values)
				if (font != null) DestroyImmediate(font);
			_fontCache.Clear();

			if (_glMaterial != null) { DestroyImmediate(_glMaterial); _glMaterial = null; }
			if (_texMaterial != null) { DestroyImmediate(_texMaterial); _texMaterial = null; }
		}

		private void SetStatus(string message)
		{
			if (_statusLabel != null) _statusLabel.text = message;
		}

		#endregion
	}
}

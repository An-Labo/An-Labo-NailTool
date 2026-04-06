using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Language;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.VisualElements {

	internal class ChangelogBanner : VisualElement {

		private const string PREF_KEY_LAST_SEEN_VERSION = "MDNailTool_LastSeenChangelogVersion";
		private const int MAX_DISPLAY_ENTRIES = 20;

		private readonly Label _summaryLabel;
		private List<ChangelogEntry>? _entries;

		public ChangelogBanner() {
			style.flexDirection = FlexDirection.Row;
			style.alignItems = Align.Center;
			style.flexShrink = 0;

			_summaryLabel = new Label { pickingMode = PickingMode.Position };
			_summaryLabel.AddToClassList("mdn-changelog-summary");
			_summaryLabel.RegisterCallback<ClickEvent>(_ => ShowDropdown());
			Add(_summaryLabel);

			Populate();
		}

		private void Populate() {
			_entries = LoadChangelog();
			if (_entries == null || _entries.Count == 0) {
				_summaryLabel.text = "";
				return;
			}

			string lang = GetLang();
			ChangelogEntry latest = _entries[0];

			string latestText = FormatEntry(latest, lang);
			bool isNew = IsNewVersion(latest.version);
			_summaryLabel.text = (isNew ? "NEW " : "") + latestText;
			if (isNew) _summaryLabel.AddToClassList("mdn-changelog-new");

		}

		private void ShowDropdown() {
			if (_entries == null || _entries.Count == 0) return;

			string lang = GetLang();

			if (_entries.Count > 0 && _entries[0].version != null) {
				EditorPrefs.SetString(PREF_KEY_LAST_SEEN_VERSION, _entries[0].version!);
				_summaryLabel.RemoveFromClassList("mdn-changelog-new");
				_summaryLabel.text = FormatEntry(_entries[0], lang);
			}

			Rect buttonRect = _summaryLabel.worldBound;
			ChangelogPopup.Show(buttonRect, _entries, lang);
		}

		private static string GetLang() {
			return LanguageManager.CurrentLanguageData.language == "jp" ? "jp" : "en";
		}

		private static string FormatEntry(ChangelogEntry entry, string lang) {
			string ver = $"v{entry.version}";
			List<string>? lines = entry.entries?.GetValueOrDefault(lang)
			                      ?? entry.entries?.GetValueOrDefault("jp");
			if (lines == null || lines.Count == 0) return ver;
			string joined = string.Join(", ", lines);
			return $"{ver}: {joined}";
		}

		private static bool IsNewVersion(string? version) {
			if (string.IsNullOrEmpty(version)) return false;
			string lastSeen = EditorPrefs.GetString(PREF_KEY_LAST_SEEN_VERSION, "");
			return lastSeen != version;
		}

		private static List<ChangelogEntry>? LoadChangelog() {
			try {
				TextAsset? asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
					MDNailToolDefines.DB_CHANGELOG_FILE_PATH);
				if (asset == null) return null;
				return JsonConvert.DeserializeObject<List<ChangelogEntry>>(asset.text);
			} catch (Exception e) {
				Debug.LogWarning($"[MDNailTool] Failed to load changelog: {e}");
				return null;
			}
		}

		[Serializable]
		internal class ChangelogEntry {
			[JsonProperty("version")] public string? version;
			[JsonProperty("date")] public string? date;
			[JsonProperty("entries")] public Dictionary<string, List<string>>? entries;
		}
	}

	internal class ChangelogPopup : EditorWindow {

		private const float POPUP_WIDTH = 400f;
		private const float POPUP_MAX_HEIGHT = 500f;

		private List<ChangelogBanner.ChangelogEntry>? _entries;
		private string _lang = "jp";

		public static void Show(Rect buttonRect, List<ChangelogBanner.ChangelogEntry> entries, string lang) {
			var window = CreateInstance<ChangelogPopup>();
			window._entries = entries;
			window._lang = lang;

			Vector2 screenPos = GUIUtility.GUIToScreenPoint(new Vector2(buttonRect.x, buttonRect.yMax));
			var size = new Vector2(POPUP_WIDTH, POPUP_MAX_HEIGHT);
			window.ShowAsDropDown(new Rect(screenPos, Vector2.zero), size);
		}

		private void CreateGUI() {
			if (_entries == null) return;

			var root = rootVisualElement;
			root.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f));

			var scroll = new ScrollView(ScrollViewMode.Vertical);
			scroll.style.flexGrow = 1;
			scroll.style.paddingTop = 4;
			scroll.style.paddingBottom = 4;
			scroll.style.paddingLeft = 8;
			scroll.style.paddingRight = 8;

			int count = 0;
			foreach (var entry in _entries) {
				if (count++ >= 50) break;

				string ver = $"v{entry.version}";
				string date = entry.date ?? "";
				List<string>? lines = entry.entries?.GetValueOrDefault(_lang)
				                      ?? entry.entries?.GetValueOrDefault("jp");

				string header = string.IsNullOrEmpty(date) ? ver : $"{ver}  ({date})";
				var headerLabel = new Label(header);
				headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
				headerLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
				headerLabel.style.marginTop = count > 1 ? 6 : 0;
				scroll.Add(headerLabel);

				if (lines != null) {
					foreach (string line in lines) {
						var lineLabel = new Label($"  {line}");
						lineLabel.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.75f));
						lineLabel.style.marginLeft = 8;
						scroll.Add(lineLabel);
					}
				}
			}

			root.Add(scroll);
		}
	}
}

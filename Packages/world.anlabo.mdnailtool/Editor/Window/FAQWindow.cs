#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Language;

namespace world.anlabo.mdnailtool.Editor.Window
{
	internal sealed class FAQWindow : EditorWindow
	{
		private const float Width = 620f;
		private const float Height = 560f;

		public static void ShowWindow(MDNailToolWindow? parentWindow = null)
		{
			CloseAll();
			FAQWindow window = CreateInstance<FAQWindow>();
			window.titleContent = new GUIContent(LanguageManager.S("window.faq_title") ?? "FAQ");
			window.minSize = new Vector2(460f, 360f);
			Rect rect = BuildWindowRect(parentWindow);
			window.ShowAsDropDown(new Rect(rect.position, Vector2.zero), new Vector2(Width, Height));
		}

		public static void CloseAll()
		{
			foreach (FAQWindow window in Resources.FindObjectsOfTypeAll<FAQWindow>())
			{
				window.Close();
			}
		}

		private static Rect BuildWindowRect(MDNailToolWindow? parentWindow)
		{
			if (parentWindow != null)
			{
				Rect parent = parentWindow.position;
				float x = parent.x + 28f;
				float y = parent.y + 42f;
				return new Rect(x, y, Width, Height);
			}

			return new Rect(120f, 120f, Width, Height);
		}

		private void CreateGUI()
		{
			StyleSheet? uss = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(MDNailToolGuids.WindowUss, MDNailToolGuids.WindowUssPath);
			if (uss != null) rootVisualElement.styleSheets.Add(uss);

			rootVisualElement.AddToClassList("mdn-faq-root");

			Label title = new(LanguageManager.S("window.faq_title") ?? "FAQ");
			title.AddToClassList("mdn-faq-title");
			rootVisualElement.Add(title);

			Label lead = new(LanguageManager.S("window.faq_lead") ?? "Please check these items first.");
			lead.AddToClassList("mdn-faq-lead");
			rootVisualElement.Add(lead);

			ScrollView scroll = new(ScrollViewMode.Vertical);
			scroll.AddToClassList("mdn-faq-scroll");
			rootVisualElement.Add(scroll);

			List<FAQEntry> entries = LoadEntries();
			if (entries.Count == 0)
			{
				Label empty = new(LanguageManager.S("window.faq_empty") ?? "No FAQ entries are bundled.");
				empty.AddToClassList("mdn-faq-empty");
				scroll.Add(empty);
			}
			else
			{
				string lang = LanguageManager.CurrentLanguageData.language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? "ja" : "en";
				foreach (FAQEntry entry in entries.OrderBy(e => e.priority))
				{
					Foldout item = new() { text = GetLocalized(entry.title, lang, entry.id), value = false };
					item.AddToClassList("mdn-faq-item");
					item.AddToClassList("mdn-faq-foldout");

					Label a = new(GetLocalized(entry.body, lang, string.Empty));
					a.AddToClassList("mdn-faq-answer");
					item.Add(a);

					scroll.Add(item);
				}
			}

			AddMoreFaqItem(scroll);
		}

		private static void AddMoreFaqItem(VisualElement parent)
		{
			VisualElement item = new();
			item.AddToClassList("mdn-faq-item");
			item.AddToClassList("mdn-faq-more-item");

			Label title = new(LanguageManager.S("window.faq_more_title") ?? "Check other FAQs");
			title.AddToClassList("mdn-faq-question");
			item.Add(title);

			Label body = new(LanguageManager.S("window.faq_more_body") ?? "Open the web FAQ for additional questions and the latest information.");
			body.AddToClassList("mdn-faq-answer");
			item.Add(body);

			Label link = new(LanguageManager.S("window.faq_more_link") ?? "Open FAQ page");
			link.AddToClassList("mdn-faq-link");
			link.RegisterCallback<ClickEvent>(_ => Application.OpenURL(LanguageManager.S("link.contact")));
			item.Add(link);

			parent.Add(item);
		}

		private static List<FAQEntry> LoadEntries()
		{
			try
			{
				TextAsset? asset = MDNailToolAssetLoader.LoadAssetSafe<TextAsset>(MDNailToolDefines.DB_FAQ_FILE_PATH);
				if (asset == null) return new List<FAQEntry>();
				List<FAQEntry>? entries = JsonConvert.DeserializeObject<List<FAQEntry>>(asset.text);
				return entries?.Where(e => e.includeInTool).ToList() ?? new List<FAQEntry>();
			}
			catch (Exception ex)
			{
				ToolConsole.Warn("NailTool", $"FAQの読み込みに失敗: {ex.Message}");
				return new List<FAQEntry>();
			}
		}

		private static string GetLocalized(Dictionary<string, string>? values, string lang, string fallback)
		{
			if (values == null) return fallback;
			if (values.TryGetValue(lang, out string? value) && !string.IsNullOrWhiteSpace(value)) return value;
			if (values.TryGetValue("ja", out value) && !string.IsNullOrWhiteSpace(value)) return value;
			if (values.TryGetValue("en", out value) && !string.IsNullOrWhiteSpace(value)) return value;
			return fallback;
		}

		[Serializable]
		private sealed class FAQEntry
		{
			public string id = string.Empty;
			public int priority;
			public bool includeInTool;
			public Dictionary<string, string>? title;
			public Dictionary<string, string>? body;
		}
	}
}

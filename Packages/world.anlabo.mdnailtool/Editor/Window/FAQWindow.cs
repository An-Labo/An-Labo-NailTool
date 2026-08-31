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
		private const string AvatarRequestUrl = "https://anlabo.world/manual/#avatar-request";
		private const string TermsUrl = "https://anlabo.world/manual/#terms";
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
			BuildContent(rootVisualElement);
		}

		internal static void BuildContent(VisualElement root)
		{
			root.AddToClassList("mdn-faq-root");

			Label title = new(LanguageManager.S("window.faq_title") ?? "FAQ");
			title.AddToClassList("mdn-faq-title");
			root.Add(title);

			Label lead = new(LanguageManager.S("window.faq_lead") ?? "Please check these items first.");
			lead.AddToClassList("mdn-faq-lead");
			root.Add(lead);

			ScrollView scroll = new(ScrollViewMode.Vertical);
			scroll.AddToClassList("mdn-faq-scroll");
			root.Add(scroll);

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
					VisualElement answerPanel = new();
					answerPanel.AddToClassList("mdn-faq-answer-panel");

					Label a = new(GetLocalized(entry.body, lang, string.Empty));
					a.AddToClassList("mdn-faq-answer");
					answerPanel.Add(a);

					string url = GetLocalized(entry.link, lang, string.Empty);
					if (!string.IsNullOrWhiteSpace(url))
					{
						Label link = new(GetLocalized(entry.linkLabel, lang, url));
						link.AddToClassList("mdn-faq-link");
						link.RegisterCallback<ClickEvent>(_ => Application.OpenURL(url));
						answerPanel.Add(link);
					}
					item.Add(answerPanel);

					scroll.Add(item);
				}

				AddExternalGuidance(scroll);
			}
		}

		private static void AddExternalGuidance(VisualElement parent)
		{
			VisualElement card = new();
			card.AddToClassList("mdn-faq-external-card");

			Label description = new(LanguageManager.S("window.faq_external_description")
			                        ?? "For avatar support requests and terms of use, please check the website.");
			description.AddToClassList("mdn-faq-external-description");
			card.Add(description);

			VisualElement links = new();
			links.AddToClassList("mdn-faq-external-links");
			links.Add(CreateExternalLink(LanguageManager.S("window.faq_avatar_request_link") ?? "Avatar support requests", AvatarRequestUrl));
			links.Add(CreateExternalLink(LanguageManager.S("window.faq_terms_link") ?? "Terms of use", TermsUrl));
			card.Add(links);
			parent.Add(card);
		}

		private static Label CreateExternalLink(string text, string url)
		{
			Label link = new(text);
			link.AddToClassList("mdn-faq-link");
			link.AddToClassList("mdn-faq-external-link");
			link.RegisterCallback<ClickEvent>(_ => Application.OpenURL(url));
			return link;
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
			public Dictionary<string, string>? link;
			public Dictionary<string, string>? linkLabel;
		}
	}
}

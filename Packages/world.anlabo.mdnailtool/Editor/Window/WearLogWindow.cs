#nullable enable

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Language;

namespace world.anlabo.mdnailtool.Editor.Window
{
	internal sealed class SupportWindow : EditorWindow
	{
		private static MDNailToolWindow? _sourceWindow;
		private static string? _expandedFaqId;

		internal static void ShowWindow(MDNailToolWindow? sourceWindow = null, string? expandedFaqId = null)
		{
			_sourceWindow = sourceWindow ?? Resources.FindObjectsOfTypeAll<MDNailToolWindow>().FirstOrDefault();
			_expandedFaqId = expandedFaqId;
			foreach (SupportWindow existing in Resources.FindObjectsOfTypeAll<SupportWindow>())
				existing.Close();
			SupportWindow window = GetWindow<SupportWindow>();
			window.titleContent = new GUIContent(LanguageManager.S("window.support_title") ?? "Help");
			window.minSize = new Vector2(620f, 700f);
			Rect current = window.position;
			window.position = new Rect(
				current.x,
				current.y,
				Math.Max(current.width, 620f),
				Math.Max(current.height, 780f));
			window.Show();
			window.Focus();
		}

		private void CreateGUI()
		{
			StyleSheet? uss = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(
				MDNailToolGuids.WindowUss, MDNailToolGuids.WindowUssPath);
			if (uss != null) rootVisualElement.styleSheets.Add(uss);

			rootVisualElement.AddToClassList("mdn-support-root");

			var header = new VisualElement();
			header.AddToClassList("mdn-support-header");
			var title = new Label(LanguageManager.S("window.support_title") ?? "Help");
			title.AddToClassList("mdn-support-title");
			header.Add(title);
			var lead = new Label(LanguageManager.S("window.support_lead") ?? "Check common questions and recent activity here.");
			lead.AddToClassList("mdn-support-lead");
			header.Add(lead);
			rootVisualElement.Add(header);

			BuildLogSection();

			var faq = new VisualElement();
			faq.AddToClassList("mdn-support-faq");
			rootVisualElement.Add(faq);
			FAQWindow.BuildContent(faq, _expandedFaqId);

		}

		private void BuildLogSection()
		{
			var card = new VisualElement();
			card.AddToClassList("mdn-support-log-card");

			var info = new VisualElement();
			info.AddToClassList("mdn-support-log-info");
			var title = new Label(LanguageManager.S("window.support_log_title") ?? "Support information");
			title.AddToClassList("mdn-support-log-title");
			info.Add(title);
			var description = new Label(LanguageManager.S("window.support_log_description") ?? "Please send this information when contacting support.");
			description.AddToClassList("mdn-support-log-description");
			info.Add(description);
			card.Add(info);

			var actions = new VisualElement();
			actions.AddToClassList("mdn-support-log-actions");
			var copy = new Button(CopyLog) { text = LanguageManager.S("window.support_log_copy") ?? "Copy support information" };
			copy.AddToClassList("mdn-support-log-button");
			actions.Add(copy);
			var saveImage = new Button(SaveForBooth) { text = LanguageManager.S("window.support_image_save") ?? "Save image for BOOTH" };
			saveImage.AddToClassList("mdn-support-log-button");
			actions.Add(saveImage);
			card.Add(actions);
			rootVisualElement.Add(card);
		}

		private static string BuildSupportInfo()
		{
			MDNailToolWindow? nailTool = _sourceWindow;
			if (nailTool == null)
				nailTool = Resources.FindObjectsOfTypeAll<MDNailToolWindow>().FirstOrDefault();
			if (nailTool != null) return nailTool.BuildSupportInfo();

			string[] lines = ToolConsole.GetHistory();
			string text = ToolConsole.GetSupportInfoSnapshot()
				?? $"--- An-Labo NailTool Support Info ---\nNailTool Version: {MDNailToolDefines.Version}\nUnity: {Application.unityVersion}\nOS: {SystemInfo.operatingSystem}\n";
			if (lines.Length > 0)
			{
				text += "\n--- NailTool Log ---\n" + string.Join("\n", lines);
			}
			return text;
		}

		private static void CopyLog()
		{
			EditorGUIUtility.systemCopyBuffer = BuildSupportInfo();
		}

		private void SaveForBooth()
		{
			string defaultName = $"NailTool_Support_{DateTime.Now:yyyyMMdd_HHmmss}.png";
			string path = EditorUtility.SaveFilePanel(
				LanguageManager.S("window.support_image_dialog") ?? "Save support image",
				string.Empty, defaultName, "png");
			if (string.IsNullOrWhiteSpace(path)) return;

			SupportInfoImageRenderer.SavePng(BuildSupportInfo(), path);
			ShowNotification(new GUIContent(LanguageManager.S("window.support_image_saved") ?? "Image saved"));
			EditorUtility.RevealInFinder(path);
		}
	}
}

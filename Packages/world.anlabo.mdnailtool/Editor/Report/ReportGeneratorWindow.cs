using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Report {
	public class ReportGeneratorWindow : EditorWindow {
		
		public static void ShowWindow() {
			ReportGeneratorWindow wnd = GetWindow<ReportGeneratorWindow>();
			wnd.titleContent = new GUIContent("Report Generator Window");

			ReportGenerator reportGenerator = new();
			wnd.Text = reportGenerator.GetText();
			wnd.SaveAction = reportGenerator.SaveReportWithDialog;
		}
		
		
		private const string GUID = "7d2a50c7e270e2149b8cc3b92677c998";

		private string? _text;
		private string Text {
			set {
				this._text = value[..5000] + " ... and more";
				if (this._reportTextField != null) {
					this._reportTextField.value = this._text;
				}
			}
		}

		private Action? SaveAction { get; set; }

		private TextField? _reportTextField;

		public void CreateGUI() {
			string uxmlPath = AssetDatabase.GUIDToAssetPath(GUID);
			VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
			TemplateContainer uxmlInstance = uxml.Instantiate();
			uxmlInstance.style.flexGrow = 1;
			uxmlInstance.style.marginTop = 5;
			uxmlInstance.style.marginRight = 5;
			uxmlInstance.style.marginBottom = 5;
			uxmlInstance.style.marginLeft = 5;
			this.rootVisualElement.Add(uxmlInstance);
			
			this._reportTextField = this.rootVisualElement.Q<TextField>("ReportText");
			this._reportTextField.value = this._text;

			this.rootVisualElement.Q<Button>("SaveButton").clicked += OnSaveButton;
		}

		private void OnSaveButton() {
			this.SaveAction?.Invoke();
		}
	}
}
using System.Collections.Generic;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Language;

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	internal class LanguageDropDown : PopupField<LanguageData> {

		public LanguageDropDown() : base(
			"Language", 
			LanguageManager.LanguageDataList,
			LanguageManager.CurrentLanguageData,
			FormatCallback,
			FormatCallback) {
			this.RegisterValueChangedCallback(ChangedValue);

		}

		private void ChangedValue(ChangeEvent<LanguageData> evt) {
			LanguageManager.ChangeLanguage(evt.newValue.language);
			if (this.panel?.visualTree == null) return;
			Queue<VisualElement> queue = new Queue<VisualElement>();
			queue.Enqueue(this.panel.visualTree);

			while (queue.Count > 0) {
				VisualElement target = queue.Dequeue();

				if (target is ILocalizedElement localizedElement) {
					localizedElement.UpdateLanguage();
				}
				
				foreach (VisualElement childElement in target.Children()) {
					queue.Enqueue(childElement);
				}
			}
		}

		private static string FormatCallback(LanguageData data) {
			return data.displayName;
		}
		
		internal new class UxmlFactory : UxmlFactory<LanguageDropDown> {}
	}
}
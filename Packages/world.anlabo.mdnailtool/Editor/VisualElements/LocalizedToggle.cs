using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Language;

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	internal class LocalizedToggle : Toggle, ILocalizedElement {
		private string _textId;
		// ReSharper disable once MemberCanBePrivate.Global
		internal string TextId {
			get => this._textId;
			set {
				if (this._textId == value) return;
				this._textId = value;
				this.UpdateLanguage();
			}
		}

		public void UpdateLanguage() {
			this.text = LanguageManager.S(this.TextId);
		}

		internal new class UxmlFactory : UxmlFactory<LocalizedToggle, UxmlTraits> { }

		internal new class UxmlTraits : Toggle.UxmlTraits {
			private readonly UxmlStringAttributeDescription _textId = new UxmlStringAttributeDescription {
				name = "text-id",
				defaultValue = ""
			};

			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc) {
				base.Init(ve, bag, cc);
				if (ve is not LocalizedToggle localizedElement) return;
				localizedElement.TextId = this._textId.GetValueFromBag(bag, cc);
			}
		}
	}
}
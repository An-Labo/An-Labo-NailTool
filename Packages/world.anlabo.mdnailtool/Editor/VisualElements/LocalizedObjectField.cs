using UnityEditor.UIElements;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Language;

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	internal class LocalizedObjectField : ObjectField, ILocalizedElement {
		private string _textId;
		private string _tooltipId = "";

		// ReSharper disable once MemberCanBePrivate.Global
		internal string TextId {
			get => this._textId;
			set {
				if (this._textId == value) return;
				this._textId = value;
				this.UpdateLanguage();
			}
		}

		internal string TooltipId {
			get => this._tooltipId;
			set {
				this._tooltipId = value;
				this.UpdateLanguage();
			}
		}

		public void UpdateLanguage() {
			this.label = LanguageManager.S(this.TextId);
			if (!string.IsNullOrEmpty(this._tooltipId))
				this.tooltip = LanguageManager.S(this._tooltipId) ?? "";
		}

		internal new class UxmlFactory : UxmlFactory<LocalizedObjectField, UxmlTraits> { }

		internal new class UxmlTraits : ObjectField.UxmlTraits {
			private readonly UxmlStringAttributeDescription _textId = new UxmlStringAttributeDescription {
				name = "text-id",
				defaultValue = ""
			};
			private readonly UxmlStringAttributeDescription _tooltipId = new UxmlStringAttributeDescription {
				name = "tooltip-id",
				defaultValue = ""
			};

			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc) {
				base.Init(ve, bag, cc);
				if (ve is not LocalizedObjectField localizedElement) return;
				localizedElement.TextId = this._textId.GetValueFromBag(bag, cc);
				localizedElement.TooltipId = this._tooltipId.GetValueFromBag(bag, cc);
			}
		}
	}
}

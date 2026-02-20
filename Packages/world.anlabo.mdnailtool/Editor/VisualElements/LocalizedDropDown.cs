using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Language;
#nullable enable

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	public class LocalizedDropDown : DropdownField, ILocalizedElement {
		private string? _textId;
		private string _tooltipId = "";

		// ReSharper disable once MemberCanBePrivate.Global
		internal string? TextId {
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

		public LocalizedDropDown() {
			this.labelElement.RegisterValueChangedCallback(DisableValueChangeEvent);
		}


		public void UpdateLanguage() {
			this.label = LanguageManager.S(this.TextId ?? "");
			if (!string.IsNullOrEmpty(this._tooltipId))
				this.tooltip = LanguageManager.S(this._tooltipId) ?? "";
		}

		private static void DisableValueChangeEvent(ChangeEvent<string> evt) {
			evt.StopPropagation();
		}

		internal new class UxmlFactory : UxmlFactory<LocalizedDropDown, UxmlTraits> { }

		internal new class UxmlTraits : DropdownField.UxmlTraits {
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
				if (ve is not LocalizedDropDown localizedElement) return;
				localizedElement.TextId = this._textId.GetValueFromBag(bag, cc);
				localizedElement.TooltipId = this._tooltipId.GetValueFromBag(bag, cc);
			}
		}
	}
}
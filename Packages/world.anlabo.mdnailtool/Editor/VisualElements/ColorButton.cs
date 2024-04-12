using UnityEngine;
using UnityEngine.UIElements;

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	internal class ColorButton : Button {
		public ColorButton() {
			this.style.width = this.style.height = 26;
			this.style.paddingTop = this.style.paddingRight = this.style.paddingBottom = this.style.paddingLeft = 0;
			this.style.borderTopLeftRadius = this.style.borderTopRightRadius = this.style.borderBottomRightRadius = this.style.borderBottomLeftRadius = 13;
			this.style.borderTopWidth = this.style.borderRightWidth = this.style.borderBottomWidth = this.style.borderLeftWidth = 2;
			this.style.borderTopColor = this.style.borderRightColor = this.style.borderBottomColor = this.style.borderLeftColor = new Color(0.7f, 0.7f, 0.7f);
		}

		internal new class UxmlFactory : UxmlFactory<ColorButton, UxmlTraits> { }

		internal new class UxmlTraits : Button.UxmlTraits {
			private readonly UxmlColorAttributeDescription _color = new UxmlColorAttributeDescription {
				name = "color",
				defaultValue = Color.white,
			};
			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc) {
				base.Init(ve, bag, cc);
				if (ve is not ColorButton colorButton) return;
				colorButton.style.backgroundColor = this._color.GetValueFromBag(bag, cc);
			}
		}
	}
}
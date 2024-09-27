using System.Collections.Generic;
using UnityEngine.UIElements;

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	public class AvatarSearchField : VisualElement {

		public AvatarSearchField() {
			this.style.height = new Length(20, LengthUnit.Pixel);
			this.style.paddingLeft = new Length(121, LengthUnit.Pixel);

			DropdownField dropdownField = new();
			dropdownField.choices = new List<string> {
				"並べ替え",
				"ショップ名順↓",
				"ショップ名順↑",
				"アバター名順↓",
				"アバター名順↑",
				"新着順↓",
				"新着順↑",
			};
			dropdownField.value = dropdownField.choices[0];
			this.Add(dropdownField);
		}
		
		internal new class UxmlFactory : UxmlFactory<AvatarSearchField, UxmlTraits> { }

		internal new class UxmlTraits : VisualElement.UxmlTraits { }
	}
}
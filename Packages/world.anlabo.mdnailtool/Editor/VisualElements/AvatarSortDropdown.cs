using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Language;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	public class AvatarSortDropdown : PopupField<AvatarSortOrder>, ILocalizedElement {

		public AvatarSortDropdown() {
			this.style.height = new Length(20, LengthUnit.Pixel);
			this.style.paddingLeft = new Length(121, LengthUnit.Pixel);
		}

		public void Init() {
			this.choices = Enum.GetValues(typeof(AvatarSortOrder)).Cast<AvatarSortOrder>().ToList();
			formatListItemCallback = FormatValueCallback;
			formatSelectedValueCallback = FormatValueCallback;
		}

		private static string FormatValueCallback(AvatarSortOrder arg) {
			MemberInfo[] memberInfo = typeof(AvatarSortOrder).GetMember(arg.ToString());
			InspectorNameAttribute attr = memberInfo[0].GetCustomAttribute<InspectorNameAttribute>();
			return LanguageManager.S(attr.displayName);
		}

		internal new class UxmlFactory : UxmlFactory<AvatarSortDropdown, UxmlTraits> { }

		internal new class UxmlTraits : VisualElement.UxmlTraits { }

		public void UpdateLanguage() {
			this.SetValueWithoutNotify(this.value);
		}
	}
}
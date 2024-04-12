using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	public class NailDesignDropDowns : VisualElement, ILocalizedElement {
		private readonly LocalizedDropDown _designPopup;
		private readonly DropdownField _materialPopup;
		private readonly DropdownField _colorPopup;

		private List<string>? _designPopupElements;
		private List<string>? _materialPopupElements;
		private List<string>? _colorPopupElements;

		private Dictionary<string, string>? _designDisplayNameDictionary;

		public NailDesignDropDowns() {
			this.style.flexDirection = FlexDirection.Row;
			Func<string, string> getDesignPopupDisplayNameFunc = this.GetDesignPopupDisplayName;
			this._designPopup = new LocalizedDropDown {
				style = {
					width = new Length(40, LengthUnit.Percent),
					marginLeft = new Length(0)
				},
				formatSelectedValueCallback = getDesignPopupDisplayNameFunc,
				formatListItemCallback = getDesignPopupDisplayNameFunc
			};
			this._designPopup.RegisterValueChangedCallback(this.OnChangeDesignPopup);

			this._materialPopup = new DropdownField {
				style = {
					flexGrow = 0,
					marginLeft = new Length(0, LengthUnit.Pixel),
					width = new Length(28, LengthUnit.Percent)
				}
			};

			this._colorPopup = new DropdownField {
				style = {
					flexGrow = 0,
					marginLeft = new Length(0, LengthUnit.Pixel),
					width = new Length(28, LengthUnit.Percent)
				}
			};

			this.Add(this._designPopup);
			this.Add(this._materialPopup);
			this.Add(this._colorPopup);

			this.Init();
		}

		private void Init() {
			using DBNailDesign dbNailDesign = new();
			this._designPopupElements = dbNailDesign.collection
				.Where(design => INailProcessor.IsInstalledDesign(design.DesignName))
				.OrderBy(nailDesign => nailDesign.Id)
				.Select(design => design.DesignName)
				.ToList();
			this._designDisplayNameDictionary = dbNailDesign.collection
				.Where(design => INailProcessor.IsInstalledDesign(design.DesignName))
				.ToDictionary(
					nailDesign => nailDesign.DesignName,
					nailDesign => nailDesign.DisplayNames == null
						? nailDesign.DesignName
						: nailDesign.DisplayNames.GetValueOrDefault(LanguageManager.CurrentLanguageData.language, nailDesign.DesignName));
			this._designPopup.choices = this._designPopupElements;
			this._designPopup.value = this._designPopupElements.Count <= 0 ? string.Empty : this._designPopupElements[0];


			NailDesign? design = dbNailDesign.FindNailDesignByDesignName(this._designPopup.value);
			if (design == null) return;

			INailProcessor nailProcessor = INailProcessor.CreateNailDesign(design.DesignName);

			if (design.MaterialVariation == null) {
				this._materialPopupElements = new List<string> { "" };
			} else {
				this._materialPopupElements = design.MaterialVariation
					.Where(pair => nailProcessor.IsInstalledMaterialVariation(pair.Value.MaterialName))
					.Select(pair => pair.Value.MaterialName)
					.ToList();
			}

			this._materialPopup.choices = this._materialPopupElements;
			this._materialPopup.value = this._materialPopupElements.Count <= 0 ? string.Empty : this._materialPopupElements[0];
			string materialName = this._materialPopup.value;

			this._colorPopupElements = design.ColorVariation
				.Where(pair => nailProcessor.IsInstalledColorVariation(materialName, pair.Value.ColorName))
				.Select(pair => pair.Value.ColorName)
				.ToList();
			this._colorPopup.choices = this._colorPopupElements;
			this._colorPopup.value = this._colorPopupElements.Count <= 0 ? string.Empty : this._colorPopupElements[0];
		}

		private void OnChangeDesignPopup(ChangeEvent<string?> evt) {
			using DBNailDesign dbNailDesign = new();
			NailDesign? design = dbNailDesign.FindNailDesignByDesignName(evt.newValue);
			if (design == null) {
				this._colorPopupElements = null;
				this._colorPopup.choices = new List<string>();
				this._colorPopup.value = string.Empty;
				return;
			}


			INailProcessor nailProcessor = INailProcessor.CreateNailDesign(design.DesignName);

			if (design.MaterialVariation == null) {
				this._materialPopupElements = new List<string> { "" };
			} else {
				this._materialPopupElements = design.MaterialVariation
					.Where(pair => nailProcessor.IsInstalledMaterialVariation(pair.Value.MaterialName))
					.Select(pair => pair.Value.MaterialName)
					.ToList();
			}

			this._materialPopup.choices = this._materialPopupElements;
			this._materialPopup.value = this._materialPopupElements.Count <= 0 ? string.Empty : this._materialPopupElements[0];
			string materialName = this._materialPopup.value;

			this._colorPopupElements = design.ColorVariation
				.Where(pair => nailProcessor.IsInstalledColorVariation(materialName, pair.Value.ColorName))
				.Select(pair => pair.Value.ColorName)
				.ToList();
			this._colorPopup.choices = this._colorPopupElements;
			this._colorPopup.value = this._colorPopupElements.Count <= 0 ? string.Empty : this._colorPopupElements[0];
		}

		private string GetDesignPopupDisplayName(string? id) {
			if (id == null) return "";
			if (this._designDisplayNameDictionary == null) return id;
			return this._designDisplayNameDictionary.GetValueOrDefault(id, id) ?? id;
		}

		public (string, string, string) GetSelectedDesignAndVariationName() {
			string designName = this._designPopup.value;
			string materialName = this._materialPopup.value;
			string colorName = this._colorPopup.value;
			return (designName, materialName, colorName);
		}

		public void SetValue(string designName, string materialName, List<string>? materialPopupElements, string colorName, List<string>? colorPopupElements) {
			this._designPopup.SetValueWithoutNotify(designName);
			this._materialPopup.SetValueWithoutNotify(materialName);
			this._materialPopup.choices = materialPopupElements;
			this._colorPopup.SetValueWithoutNotify(colorName);
			this._colorPopup.choices = colorPopupElements;
		}

		public void SetMaterialValue(string materialName) {
			this._materialPopup.SetValueWithoutNotify(materialName);
		}

		public void SetColorValue(string colorName) {
			this._colorPopup.SetValueWithoutNotify(colorName);
		}

		public void UpdateLanguage() {
			using DBNailDesign dbNailDesign = new();
			this._designDisplayNameDictionary = dbNailDesign.collection
				.Where(design => INailProcessor.IsInstalledDesign(design.DesignName))
				.ToDictionary(
					nailDesign => nailDesign.DesignName,
					nailDesign => nailDesign.DisplayNames == null
						? nailDesign.DesignName
						: nailDesign.DisplayNames!.GetValueOrDefault(LanguageManager.CurrentLanguageData.language, nailDesign.DesignName));
			string? oldValue = this._designPopup.value;
			this._designPopup.SetValueWithoutNotify("");
			this._designPopup.SetValueWithoutNotify(oldValue);
		}

		internal new class UxmlFactory : UxmlFactory<NailDesignDropDowns, UxmlTraits> { }

		internal new class UxmlTraits : VisualElement.UxmlTraits {
			private readonly UxmlStringAttributeDescription _textId = new() {
				name = "text-id",
				defaultValue = ""
			};

			public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc) {
				base.Init(ve, bag, cc);
				if (ve is not NailDesignDropDowns localizedElement) return;
				localizedElement._designPopup.TextId = this._textId.GetValueFromBag(bag, cc);
			}
		}
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	public class NailDesignDropDowns : VisualElement, ILocalizedElement {
		private readonly Label _fingerNameLabel;
		private readonly LocalizedDropDown _designPopup;
		private readonly DropdownField _materialPopup;
		private readonly DropdownField _colorPopup;
		private readonly DropdownField _additionalMaterialPopup;
		private readonly DropdownField _additionalObjectPopup;

		private string? _fingerTextId;

		private List<string>? _designPopupElements;
		private List<string>? _materialPopupElements;
		private List<string>? _colorPopupElements;

		private Dictionary<string, string>? _designDisplayNameDictionary;

		// ---- 指インデックス (0-19) ----
		private int _fingerIndex = -1;
		internal void SetFingerIndex(int fingerIndex) => this._fingerIndex = fingerIndex;
		internal int GetFingerIndex() => this._fingerIndex;

		// ---- バリアントモード ----
		private bool _isVariantMode;
		private string? _selectedVariantDesignName;
		private Dictionary<string, string>? _variantDisplayToDesign;

		public NailDesignDropDowns() {
			this.style.flexDirection = FlexDirection.Row;
			this.style.alignItems = Align.Center;

			// 指名ラベル（デザインドロップダウンから分離）
			this._fingerNameLabel = new Label {
				style = {
					overflow = Overflow.Hidden,
					unityTextAlign = TextAnchor.MiddleLeft,
					marginRight = new Length(2, LengthUnit.Pixel),
				}
			};
			this._fingerNameLabel.AddToClassList("mdn-finger-name-label");

			Func<string, string> getDesignPopupDisplayNameFunc = this.GetDesignPopupDisplayName;
			this._designPopup = new LocalizedDropDown {
				style = {
					marginLeft = new Length(0)
				},
				formatSelectedValueCallback = getDesignPopupDisplayNameFunc,
				formatListItemCallback = getDesignPopupDisplayNameFunc,
				name = "NailDesignDropDowns-DesignDropDown",
			};
			this._designPopup.AddToClassList("mdn-finger-design-dropdown");
			this._designPopup.RegisterValueChangedCallback(this.OnChangeDesignPopup);

			this._materialPopup = new DropdownField {
				style = {
					flexGrow = 0,
					marginLeft = new Length(0, LengthUnit.Pixel),
				}
			};
			this._materialPopup.AddToClassList("mdn-finger-mat-dropdown");
			this._materialPopup.RegisterValueChangedCallback(this.OnChangeMaterialPopup);

			this._colorPopup = new DropdownField {
				style = {
					flexGrow = 0,
					marginLeft = new Length(0, LengthUnit.Pixel),
				}
			};
			this._colorPopup.AddToClassList("mdn-finger-col-dropdown");

			this._additionalMaterialPopup = new DropdownField {
				style = {
					flexGrow = 0,
					marginLeft = new Length(0, LengthUnit.Pixel),
				}
			};
			this._additionalMaterialPopup.AddToClassList("mdn-finger-addmat-dropdown");

			this._additionalObjectPopup = new DropdownField {
				style = {
					flexGrow = 0,
					marginLeft = new Length(0, LengthUnit.Pixel),
				}
			};
			this._additionalObjectPopup.AddToClassList("mdn-finger-addobj-dropdown");

			this.Add(this._fingerNameLabel);
			this.Add(this._designPopup);
			this.Add(this._materialPopup);
			this.Add(this._colorPopup);
			this.Add(this._additionalMaterialPopup);
			this.Add(this._additionalObjectPopup);

			AddArrowKeyNavigation(this._designPopup);
			AddArrowKeyNavigation(this._materialPopup);
			AddArrowKeyNavigation(this._colorPopup);
			AddArrowKeyNavigation(this._additionalMaterialPopup);
			AddArrowKeyNavigation(this._additionalObjectPopup);

			this.Init();
		}

		/// <summary>
		/// 指名テキストを設定する（動的に生成された足のドロップダウン用）
		/// </summary>
		internal void SetFingerName(string textId) {
			this._fingerTextId = textId;
			this._fingerNameLabel.text = LanguageManager.S(textId) ?? textId;
		}

		internal static void AddArrowKeyNavigation(DropdownField dropdown) {
			dropdown.RegisterCallback<KeyDownEvent>(evt => {
				if (dropdown.choices == null || dropdown.choices.Count == 0) return;
				int idx = dropdown.index;
				if (evt.keyCode == KeyCode.UpArrow && idx > 0) {
					dropdown.value = dropdown.choices[idx - 1];
					evt.StopPropagation();
					evt.PreventDefault();
				} else if (evt.keyCode == KeyCode.DownArrow && idx < dropdown.choices.Count - 1) {
					dropdown.value = dropdown.choices[idx + 1];
					evt.StopPropagation();
					evt.PreventDefault();
				}
			});
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

			// バリアントチェック
			if (this.TryEnterVariantMode(dbNailDesign, design, design.DesignName)) return;

			// 通常モード
			this._isVariantMode = false;
			this._selectedVariantDesignName = null;

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
			this._materialPopup.SetValueWithoutNotify(this._materialPopupElements.Count <= 0 ? string.Empty : this._materialPopupElements[0]);
			string materialName = this._materialPopup.value;

			this._colorPopupElements = design.ColorVariation
				.Where(pair => nailProcessor.IsInstalledColorVariation(materialName, pair.Value.ColorName))
				.Select(pair => pair.Value.ColorName)
				.ToList();
			this._colorPopup.choices = this._colorPopupElements;
			this._colorPopup.SetValueWithoutNotify(this._colorPopupElements.Count <= 0 ? string.Empty : this._colorPopupElements[0]);
		}

		private void OnChangeDesignPopup(ChangeEvent<string?> evt) {
			using DBNailDesign dbNailDesign = new();
			NailDesign? design = dbNailDesign.FindNailDesignByDesignName(evt.newValue);
			if (design == null) {
				this._isVariantMode = false;
				this._selectedVariantDesignName = null;
				this._colorPopupElements = null;
				this._colorPopup.choices = new List<string>();
				this._colorPopup.SetValueWithoutNotify(string.Empty);
				return;
			}

			// バリアントチェック
			if (this.TryEnterVariantMode(dbNailDesign, design, design.DesignName)) return;

			// 通常モード
			this._isVariantMode = false;
			this._selectedVariantDesignName = null;
			this._variantDisplayToDesign = null;
			this._materialPopup.formatListItemCallback = null;
			this._materialPopup.formatSelectedValueCallback = null;

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
			this._materialPopup.SetValueWithoutNotify(this._materialPopupElements.Count <= 0 ? string.Empty : this._materialPopupElements[0]);
			string materialName = this._materialPopup.value;

			this._colorPopupElements = design.ColorVariation
				.Where(pair => nailProcessor.IsInstalledColorVariation(materialName, pair.Value.ColorName))
				.Select(pair => pair.Value.ColorName)
				.ToList();
			this._colorPopup.choices = this._colorPopupElements;
			this._colorPopup.SetValueWithoutNotify(this._colorPopupElements.Count <= 0 ? string.Empty : this._colorPopupElements[0]);
		}

		/// <summary>
		/// バリアントモードへの移行を試みる。バリアントがあればtrue、なければfalse。
		/// </summary>
		private bool TryEnterVariantMode(DBNailDesign dbNailDesign, NailDesign design, string selectedDesignName) {
			string? parentName = design.ParentVariant;
			string parentDesignName = !string.IsNullOrEmpty(parentName) ? parentName! : selectedDesignName;
			IReadOnlyList<NailDesign> children = dbNailDesign.FindChildVariants(parentDesignName);

			if (children.Count == 0) return false;

			this._isVariantMode = true;
			this.BuildVariantChoices(dbNailDesign, parentDesignName, children, selectedDesignName);
			this.RebuildColorForVariant(this._selectedVariantDesignName ?? selectedDesignName);
			return true;
		}

		/// <summary>
		/// バリアント選択肢をマテリアルDropdownに構築する。
		/// </summary>
		private void BuildVariantChoices(DBNailDesign dbNailDesign, string parentDesignName, IReadOnlyList<NailDesign> children, string selectedDesignName) {
			string langKey = LanguageManager.CurrentLanguageData.language;
			this._variantDisplayToDesign = new Dictionary<string, string>();
			var variantChoices = new List<string>();

			// 親
			NailDesign? parentDesign = dbNailDesign.FindNailDesignByDesignName(parentDesignName);
			if (parentDesign != null) {
				string pDisplay = parentDesign.DisplayNames?.GetValueOrDefault(langKey, parentDesignName) ?? parentDesignName;
				this._variantDisplayToDesign[parentDesignName] = pDisplay;
				variantChoices.Add(parentDesignName);
			}

			// 子
			foreach (NailDesign child in children) {
				if (this._variantDisplayToDesign.ContainsKey(child.DesignName)) continue;
				string cDisplay = child.DisplayNames?.GetValueOrDefault(langKey, child.DesignName) ?? child.DesignName;
				this._variantDisplayToDesign[child.DesignName] = cDisplay;
				variantChoices.Add(child.DesignName);
			}

			// 表示名フォーマッタを設定
			var displayMap = this._variantDisplayToDesign;
			this._materialPopup.formatListItemCallback = name =>
				name != null && displayMap.TryGetValue(name, out string? dn) ? dn : name ?? "";
			this._materialPopup.formatSelectedValueCallback = name =>
				name != null && displayMap.TryGetValue(name, out string? dn) ? dn : name ?? "";

			this._materialPopup.choices = variantChoices;

			// 選択されたバリアントをセット
			if (variantChoices.Contains(selectedDesignName)) {
				this._materialPopup.SetValueWithoutNotify(selectedDesignName);
				this._selectedVariantDesignName = selectedDesignName;
			} else {
				this._materialPopup.SetValueWithoutNotify(variantChoices.FirstOrDefault() ?? "");
				this._selectedVariantDesignName = variantChoices.FirstOrDefault() ?? selectedDesignName;
			}
		}

		/// <summary>
		/// 指定バリアントデザインのカラー選択肢を再構築する。
		/// </summary>
		private void RebuildColorForVariant(string variantDesignName) {
			using DBNailDesign dbNailDesign = new();
			NailDesign? design = dbNailDesign.FindNailDesignByDesignName(variantDesignName);
			if (design == null) return;

			INailProcessor nailProcessor = INailProcessor.CreateNailDesign(design.DesignName);
			string materialName = design.MaterialVariation?.Values.FirstOrDefault()?.MaterialName ?? "";

			this._colorPopupElements = design.ColorVariation
				.Where(pair => nailProcessor.IsInstalledColorVariation(materialName, pair.Value.ColorName))
				.Select(pair => pair.Value.ColorName)
				.ToList();
			this._colorPopup.choices = this._colorPopupElements;
			this._colorPopup.SetValueWithoutNotify(this._colorPopupElements.Count > 0 ? this._colorPopupElements[0] : "");
		}

		/// <summary>
		/// マテリアルDropdownの変更コールバック。バリアントモード時のみバリアント切替として処理する。
		/// </summary>
		private void OnChangeMaterialPopup(ChangeEvent<string?> evt) {
			if (!this._isVariantMode || this._variantDisplayToDesign == null || evt.newValue == null) return;

			// バリアントモード: 選択されたバリアントデザインに切り替え
			if (this._variantDisplayToDesign.ContainsKey(evt.newValue)) {
				this._selectedVariantDesignName = evt.newValue;
				// デザインPopupも同期（コールバックは発火させない）
				this._designPopup.SetValueWithoutNotify(evt.newValue);
				this.RebuildColorForVariant(evt.newValue);
			}
		}

		private string GetDesignPopupDisplayName(string? id) {
			if (id == null) return "";
			if (this._designDisplayNameDictionary == null) return id;
			return this._designDisplayNameDictionary.GetValueOrDefault(id, id) ?? id;
		}

		public (string, string, string) GetSelectedDesignAndVariationName() {
			if (this._isVariantMode && !string.IsNullOrEmpty(this._selectedVariantDesignName)) {
				// バリアントモード: バリアントデザイン名 + そのデザインの最初のマテリアル
				string designName = this._selectedVariantDesignName!;
				using DBNailDesign db = new();
				NailDesign? d = db.FindNailDesignByDesignName(designName);
				string materialName = d?.MaterialVariation?.Values.FirstOrDefault()?.MaterialName ?? "";
				string colorName = this._colorPopup.value;
				return (designName, materialName, colorName);
			}
			string dn = this._designPopup.value;
			string mn = this._materialPopup.value;
			string cn = this._colorPopup.value;
			return (dn, mn, cn);
		}

		public string GetSelectedDesignName() {
			if (this._isVariantMode && !string.IsNullOrEmpty(this._selectedVariantDesignName))
				return this._selectedVariantDesignName!;
			return this._designPopup.value;
		}

		public string? GetSelectedAdditionalMaterialSource() {
			string value = this._additionalMaterialPopup.value;
			if (string.IsNullOrEmpty(value) || this._additionalMaterialPopup.choices == null ||
			    this._additionalMaterialPopup.choices.Count == 0) return null;
			if (this._additionalMaterialPopup.index == 0) return null;
			return value;
		}

		public void SetAdditionalMaterialSource(string? value) {
			if (value == null) {
				if (this._additionalMaterialPopup.choices is { Count: > 0 })
					this._additionalMaterialPopup.SetValueWithoutNotify(this._additionalMaterialPopup.choices[0]);
			} else {
				this._additionalMaterialPopup.SetValueWithoutNotify(value);
			}
		}

		public void SetAdditionalMaterialChoices(List<string> choices) {
			this._additionalMaterialPopup.choices = choices;
			if (choices.Count > 0) {
				this._additionalMaterialPopup.SetValueWithoutNotify(choices[0]);
			}
		}

		public string? GetSelectedAdditionalObjectSource() {
			string value = this._additionalObjectPopup.value;
			if (string.IsNullOrEmpty(value) || this._additionalObjectPopup.choices == null ||
			    this._additionalObjectPopup.choices.Count == 0) return null;
			if (this._additionalObjectPopup.index == 0) return null;
			return value;
		}

		public void SetAdditionalObjectSource(string? value) {
			if (value == null) {
				if (this._additionalObjectPopup.choices is { Count: > 0 })
					this._additionalObjectPopup.SetValueWithoutNotify(this._additionalObjectPopup.choices[0]);
			} else {
				this._additionalObjectPopup.SetValueWithoutNotify(value);
			}
		}

		public void SetAdditionalObjectChoices(List<string> choices) {
			this._additionalObjectPopup.choices = choices;
			if (choices.Count > 0) {
				this._additionalObjectPopup.SetValueWithoutNotify(choices[0]);
			}
		}

		public void SetValue(string designName, string materialName, List<string>? materialPopupElements, string colorName, List<string>? colorPopupElements) {
			// バリアントチェック
			using DBNailDesign dbNailDesign = new();
			NailDesign? design = dbNailDesign.FindNailDesignByDesignName(designName);
			if (design != null) {
				string? parentName = design.ParentVariant;
				string parentDesignName = !string.IsNullOrEmpty(parentName) ? parentName! : designName;
				IReadOnlyList<NailDesign> children = dbNailDesign.FindChildVariants(parentDesignName);

				if (children.Count > 0) {
					// バリアントモード
					this._isVariantMode = true;
					this._designPopup.SetValueWithoutNotify(designName);
					this.BuildVariantChoices(dbNailDesign, parentDesignName, children, designName);
					this._colorPopup.SetValueWithoutNotify(colorName);
					this._colorPopup.choices = colorPopupElements;
					return;
				}
			}

			// 通常モード
			this._isVariantMode = false;
			this._selectedVariantDesignName = null;
			this._variantDisplayToDesign = null;
			this._materialPopup.formatListItemCallback = null;
			this._materialPopup.formatSelectedValueCallback = null;

			this._designPopup.SetValueWithoutNotify(designName);
			this._materialPopup.SetValueWithoutNotify(materialName);
			this._materialPopup.choices = materialPopupElements;
			this._colorPopup.SetValueWithoutNotify(colorName);
			this._colorPopup.choices = colorPopupElements;
		}

		public void SetMaterialValue(string materialName) {
			// バリアントモード中はマテリアル変更を無視（バリアント選択として機能するため）
			if (this._isVariantMode) return;
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

			// バリアントモードの場合、表示名マップも更新
			if (this._isVariantMode && this._selectedVariantDesignName != null) {
				NailDesign? design = dbNailDesign.FindNailDesignByDesignName(this._selectedVariantDesignName);
				if (design != null) {
					this.TryEnterVariantMode(dbNailDesign, design, this._selectedVariantDesignName);
				}
			}

			// 指名ラベルも言語更新
			if (this._fingerTextId != null)
				this._fingerNameLabel.text = LanguageManager.S(this._fingerTextId) ?? this._fingerTextId;
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
				string textId = this._textId.GetValueFromBag(bag, cc);
				localizedElement._fingerTextId = textId;
				localizedElement._fingerNameLabel.text = LanguageManager.S(textId) ?? textId;
			}
		}
	}
}

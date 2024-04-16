using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Model;
using Avatar = world.anlabo.mdnailtool.Editor.Entity.Avatar;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	internal class AvatarDropDowns : VisualElement, ILocalizedElement {
		private readonly DropdownField _shopPopup;
		private readonly DropdownField _avatarPopup;
		private readonly DropdownField _variantPopup;

		private List<string>? _shopPopupElements;
		private List<string>? _avatarPopupElements;
		private List<string>? _variantPopupElements;

		private Dictionary<string, string>? _shopDisplayNameDictionary;
		private Dictionary<string, string>? _avatarDisplayNameDictionary;
		private Dictionary<string, string>? _variantDisplayNameDictionary;

		public AvatarDropDowns() {
			LocalizedLabel label = new() {
				TextId = "window.supported_avatars"
			};
			label.UpdateLanguage();
			VisualElement searchButton = new() {
				style = {
					width = new Length(15, LengthUnit.Pixel),
					height = new Length(15, LengthUnit.Pixel),
					backgroundImage = EditorGUIUtility.Load("d_Search Icon") as Texture2D,
					visibility = Visibility.Hidden
				}
			};


			VisualElement labelGroup = new() {
				style = {
					minWidth = new Length(150, LengthUnit.Pixel),
					justifyContent = Justify.FlexStart,
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					flexShrink = 0
				}
			};
			labelGroup.Add(label);
			labelGroup.Add(searchButton);
			Func<string?, string> getShopPopupDisplayNameFunc = this.GetShopPopupDisplayName;
			this._shopPopup = new DropdownField {
				style = {
					flexGrow = 1
				},
				formatSelectedValueCallback = getShopPopupDisplayNameFunc,
				formatListItemCallback = getShopPopupDisplayNameFunc
			};
			this._shopPopup.RegisterValueChangedCallback(this.OnChangeShopPopup);


			VisualElement shopGroup = new() {
				style = {
					width = new Length(40, LengthUnit.Percent),
					flexDirection = FlexDirection.Row
				},
			};
			shopGroup.Add(labelGroup);
			shopGroup.Add(this._shopPopup);


			Func<string?, string> getAvatarPopupDisplayNameFunc = this.GetAvatarPopupDisplayName;
			this._avatarPopup = new DropdownField {
				style = {
					width = new Length(30, LengthUnit.Percent)
				},
				formatSelectedValueCallback = getAvatarPopupDisplayNameFunc,
				formatListItemCallback = getAvatarPopupDisplayNameFunc
			};
			this._avatarPopup.RegisterValueChangedCallback(this.OnChangeAvatarPopup);

			Func<string?, string> getVariantPopupDisplayNameFunc = this.GetVariantPopupDisplayName;
			this._variantPopup = new DropdownField {
				style = {
					width = StyleKeyword.Auto,
					flexGrow = 1
				},
				formatSelectedValueCallback = getVariantPopupDisplayNameFunc,
				formatListItemCallback = getVariantPopupDisplayNameFunc
			};
			this.Add(shopGroup);
			this.Add(this._avatarPopup);
			this.Add(this._variantPopup);

			this.style.flexDirection = FlexDirection.Row;

			this.Init();
		}

		private void Init() {
			string langKey = LanguageManager.CurrentLanguageData.language;
			using DBShop dbShop = new();
			this._shopPopupElements = dbShop.collection.Select(shop => shop.ShopName).ToList();
			this._shopDisplayNameDictionary = dbShop.collection
				.ToDictionary(shop => shop.ShopName, shop => shop.DisplayNames.GetValueOrDefault(langKey, shop.ShopName));
			this._shopPopup.choices = this._shopPopupElements;
			this._shopPopup.value = this._shopPopupElements?[0];

			Shop? shop = dbShop.FindShopByName(this._shopPopup.value);
			if (shop == null) return;

			this._avatarPopupElements = shop.Avatars.Values.Select(avatar => avatar.AvatarName).ToList();
			this._avatarDisplayNameDictionary = shop.Avatars.Values
				.ToDictionary(avatar => avatar.AvatarName, avatar => avatar.DisplayNames.GetValueOrDefault(langKey, avatar.AvatarName));
			this._avatarPopup.choices = this._avatarPopupElements;
			this._avatarPopup.value = this._avatarPopupElements[0];

			Avatar? avatar = shop.FindAvatarByName(this._avatarPopup.value);
			if (avatar == null) return;

			this._variantPopupElements = avatar.AvatarVariations.Values.Select(variation => variation.VariationName).ToList();
			this._variantDisplayNameDictionary = avatar.AvatarVariations.Values
				.ToDictionary(variation => variation.VariationName, variation => variation.DisplayNames.GetValueOrDefault(langKey, variation.VariationName));
			this._variantPopup.choices = this._variantPopupElements;
			this._variantPopup.value = this._variantPopupElements?[0];
		}

		public void SetValues(Shop shop, Avatar avatar, AvatarVariation avatarVariation) {
			string langKey = LanguageManager.CurrentLanguageData.language;
			
			this._shopPopup.SetValueWithoutNotify(shop.ShopName);
			
			this._avatarPopupElements = shop.Avatars.Values.Select(_avatar => _avatar.AvatarName).ToList();
			this._avatarDisplayNameDictionary = shop.Avatars.Values
				.ToDictionary(_avatar => _avatar.AvatarName, _avatar => _avatar.DisplayNames.GetValueOrDefault(langKey, _avatar.AvatarName));
			this._avatarPopup.choices = this._avatarPopupElements;
			this._avatarPopup.SetValueWithoutNotify(avatar.AvatarName);
			
			this._variantPopupElements = avatar.AvatarVariations.Values.Select(variation => variation.VariationName).ToList();
			this._variantDisplayNameDictionary = avatar.AvatarVariations.Values
				.ToDictionary(variation => variation.VariationName, variation => variation.DisplayNames.GetValueOrDefault(langKey, variation.VariationName));
			this._variantPopup.choices = this._variantPopupElements;
			this._variantPopup.SetValueWithoutNotify(avatarVariation.VariationName);
		}

		private void OnChangeShopPopup(ChangeEvent<string?> evt) {
			string langKey = LanguageManager.CurrentLanguageData.language;
			using DBShop dbShop = new();
			Shop? shop = dbShop.FindShopByName(evt.newValue);
			if (shop == null) {
				this._avatarPopupElements = null;
				this._avatarDisplayNameDictionary = null;
				this._avatarPopup.choices = new List<string?>();
				this._avatarPopup.value = null;
				return;
			}

			this._avatarPopupElements = shop.Avatars.Values.Select(avatar => avatar.AvatarName).ToList();
			this._avatarDisplayNameDictionary = shop.Avatars.Values
				.ToDictionary(avatar => avatar.AvatarName, avatar => avatar.DisplayNames.GetValueOrDefault(langKey, avatar.AvatarName));
			this._avatarPopup.choices = this._avatarPopupElements;
			this._avatarPopup.value = this._avatarPopupElements?[0];
		}

		private void OnChangeAvatarPopup(ChangeEvent<string?> evt) {
			string langKey = LanguageManager.CurrentLanguageData.language;
			using DBShop dbShop = new();
			Shop? shop = dbShop.FindShopByName(this._shopPopup.value);
			Avatar? avatar = shop?.FindAvatarByName(evt.newValue);
			if (avatar == null) {
				this._variantPopupElements = null;
				this._variantDisplayNameDictionary = null;
				this._variantPopup.choices = new List<string?>();
				this._variantPopup.value = null;
				return;
			}

			this._variantPopupElements = avatar.AvatarVariations.Values.Select(variation => variation.VariationName).ToList();
			this._variantDisplayNameDictionary = avatar.AvatarVariations.Values
				.ToDictionary(variation => variation.VariationName, variation => variation.DisplayNames.GetValueOrDefault(langKey, variation.VariationName));
			this._variantPopup.choices = this._variantPopupElements;
			this._variantPopup.value = this._variantPopupElements?[0];
		}

		private string GetShopPopupDisplayName(string? id) {
			if (id == null) return "";
			if (this._shopDisplayNameDictionary == null) return id;
			return this._shopDisplayNameDictionary.GetValueOrDefault(id, id) ?? id;
		}

		private string GetAvatarPopupDisplayName(string? id) {
			if (id == null) return "";
			if (this._avatarDisplayNameDictionary == null) return id;
			return this._avatarDisplayNameDictionary.GetValueOrDefault(id, id) ?? id;
		}

		private string GetVariantPopupDisplayName(string? id) {
			if (id == null) return "";
			if (this._variantDisplayNameDictionary == null) return id;
			return this._variantDisplayNameDictionary.GetValueOrDefault(id, id) ?? id;
		}

		public GameObject? GetSelectedPrefab() {
			string? shopName = this._shopPopup.value;
			string? avatarName = this._avatarPopup.value;
			string? variantName = this._variantPopup.value;

			if (string.IsNullOrEmpty(shopName) || string.IsNullOrEmpty(avatarName) || string.IsNullOrEmpty(variantName)) return null;

			using DBShop dbShop = new();
			Shop? shop = dbShop.FindShopByName(shopName);
			Avatar? avatar = shop?.FindAvatarByName(avatarName);
			AvatarVariation? variation = avatar?.FindAvatarVariation(variantName);
			if (variation == null) return null;

			string guid = variation.NailPrefabGUID;
			if (string.IsNullOrEmpty(guid)) return null;

			string? path = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(path)) return null;

			GameObject? nailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			return nailPrefab;
		}

		public string GetAvatarName() {
			return this._avatarPopup.value;
		}
		

		public void UpdateLanguage() {
			string langKey = LanguageManager.CurrentLanguageData.language;
			using DBShop dbShop = new();
			this._shopDisplayNameDictionary = dbShop.collection.ToDictionary(shop => shop.ShopName, shop => shop.DisplayNames.GetValueOrDefault(langKey, shop.ShopName));
			string? oldValue = this._shopPopup.value;
			this._shopPopup.SetValueWithoutNotify("");
			this._shopPopup.SetValueWithoutNotify(oldValue);

			Shop? shop = dbShop.FindShopByName(this._shopPopup.value);
			if (shop == null) return;

			this._avatarDisplayNameDictionary = shop.Avatars.Values.ToDictionary(avatar => avatar.AvatarName, avatar => avatar.DisplayNames.GetValueOrDefault(langKey, avatar.AvatarName));
			oldValue = this._avatarPopup.value;
			this._avatarPopup.SetValueWithoutNotify("");
			this._avatarPopup.SetValueWithoutNotify(oldValue);

			Avatar? avatar = shop.FindAvatarByName(this._avatarPopup.value);
			if (avatar == null) return;

			this._variantDisplayNameDictionary = avatar.AvatarVariations.Values.ToDictionary(variation => variation.VariationName, variation => variation.DisplayNames.GetValueOrDefault(langKey, variation.VariationName));
			oldValue = this._variantPopup.value;
			this._variantPopup.SetValueWithoutNotify("");
			this._variantPopup.SetValueWithoutNotify(oldValue);
		}

		internal new class UxmlFactory : UxmlFactory<AvatarDropDowns, UxmlTraits> { }

		internal new class UxmlTraits : VisualElement.UxmlTraits { }
	}
}
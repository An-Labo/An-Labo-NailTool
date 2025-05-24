using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Model;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	using TreeViewItem = TreeViewItemData<AvatarTreeView.TreeItemData>;

	internal class AvatarTreeView : TreeView {
		private string _filter = "";

		public void Init() {
			this.SetRootItems(BuildTree());
			this.makeItem = MakeItem;
			this.bindItem = this.BindItem;
		}

		public void SetFilter(string filter) {
			if (filter == this._filter) return;
			this._filter = filter;

			List<TreeViewItem> rootItems = BuildTree(this._filter);
			this.SetRootItems(rootItems);
			this.RefreshItems();
			this.ExpandAll();
		}

		private static List<TreeViewItem> BuildTree(string? filter = null) {
			int id = 0;
			string langKey = LanguageManager.CurrentLanguageData.language;
			bool nonFilter = string.IsNullOrWhiteSpace(filter);

			return BuildShopTree();

			List<TreeViewItem> BuildShopTree() {
				using DBShop _dbShop = new();
				IEnumerable<TreeViewItem> shopItems = _dbShop.collection.Select(shop => {
					string displayName = shop.DisplayNames.GetValueOrDefault(langKey, shop.ShopName);
					TreeItemData data = new() {
						type = ItemType.Shop,
						name = displayName,
						allNames = shop.DisplayNames?.Values.ToArray(),
						url = shop.shopUrl,
						shop = shop
					};
					bool matched = nonFilter || data.IsMatch(filter!);
					List<TreeViewItem> avatarItems = BuildAvatarTree(shop, matched);
					return new TreeViewItem(id++, data, avatarItems);
				});

				if (!nonFilter) {
					shopItems = shopItems.Where(item => item.hasChildren);
				}

				return shopItems.ToList();
			}

			List<TreeViewItem> BuildAvatarTree(Shop shop, bool parentMatched) {
				IEnumerable<TreeViewItem> avatarItems = shop.Avatars.Values.Select(avatar => {
					string displayName = avatar.DisplayNames.GetValueOrDefault(langKey, avatar.AvatarName);
					TreeItemData data = new() {
						type = ItemType.Avatar,
						name = displayName,
						allNames = avatar.DisplayNames?.Values.ToArray(),
						url = avatar.Url,
						shop = shop,
						avatar = avatar
					};
					bool matched = nonFilter || parentMatched || data.IsMatch(filter!);
					List<TreeViewItem> variationItems = BuildVariationTree(shop, avatar, matched);
					return new TreeViewItem(id++, data, variationItems);
				});
				if (!nonFilter && !parentMatched) {
					avatarItems = avatarItems.Where(item => item.hasChildren);
				}

				return avatarItems.ToList();
			}

			List<TreeViewItem> BuildVariationTree(Shop shop, Avatar avatar, bool parentMatched) {
				IEnumerable<TreeViewItem> variationItems = avatar.AvatarVariations.Values.Select(variation => {
					string variationName = variation.DisplayNames.GetValueOrDefault(langKey, variation.VariationName);
					TreeItemData data = new() {
						type = ItemType.Variation,
						name = variationName,
						allNames = variation.DisplayNames?.Values.ToArray(),
						shop = shop,
						avatar = avatar,
						variation = variation
					};
					return new TreeViewItem(id++, data);
				});
				if (!nonFilter && !parentMatched) {
					variationItems = variationItems.Where(item => item.data.IsMatch(filter!));
				}

				return variationItems.ToList();
			}
		}


		private static VisualElement MakeItem() {
			Label label = new() {
				name = "label",
				enableRichText = true
			};
			return label;
		}

		private void BindItem(VisualElement element, int index) {
			string elementText = this.GetItemDataForIndex<TreeItemData>(index).name;
			if (string.IsNullOrWhiteSpace(this._filter)) {
				element.Q<Label>().text = elementText;
				return;
			}

			int textIndex = elementText.IndexOf(this._filter, StringComparison.Ordinal);
			if (textIndex < 0) {
				element.Q<Label>().text = elementText;
				return;
			}

			string beforeText = elementText[..textIndex];
			string matchText = elementText[textIndex..(textIndex + this._filter.Length)];
			string afterText = elementText[(textIndex + this._filter.Length)..];
			element.Q<Label>().text = $"{beforeText}<color=red><b>{matchText}</b></color>{afterText}";
		}

		internal enum ItemType {
			Shop,
			Avatar,
			Variation
		}

		internal struct TreeItemData {
			public ItemType type;
			public string name;
			public string[]? allNames;
			public string? url;

			public Shop shop;
			public Avatar? avatar;
			public AvatarVariation? variation;

			public bool IsMatch(string filter) {
				if (this.name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;

				if (this.allNames != null) {
					if (this.allNames.Any(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase))) return true;
				}

				if (this.url == null) return false;
				if (this.url.Contains(filter)) return true;

				return false;
			}
		}

		internal new class UxmlFactory : UxmlFactory<AvatarTreeView, UxmlTraits> { }

		internal new class UxmlTraits : TreeView.UxmlTraits { }
	}
}
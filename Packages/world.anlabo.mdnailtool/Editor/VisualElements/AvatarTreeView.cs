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
					string shopName = shop.DisplayNames.GetValueOrDefault(langKey, shop.ShopName);
					bool matched = nonFilter || shopName.Contains(filter!);
					List<TreeViewItem> avatarItems = BuildAvatarTree(shop.Avatars.Values, matched);
					return new TreeViewItem(id++, new TreeItemData { name = shopName }, avatarItems);
				});

				if (!nonFilter) {
					shopItems = shopItems.Where(item => item.hasChildren);
				}

				return shopItems.ToList();
			}

			List<TreeViewItem> BuildAvatarTree(IEnumerable<Avatar> avatars, bool parentMatched) {
				IEnumerable<TreeViewItem> avatarItems = avatars.Select(avatar => {
					string avatarName = avatar.DisplayNames.GetValueOrDefault(langKey, avatar.AvatarName);
					bool matched = nonFilter || parentMatched || avatarName.Contains(filter!);
					List<TreeViewItem> variationItems = BuildVariationTree(avatar.AvatarVariations.Values, matched);
					return new TreeViewItem(id++, new TreeItemData { name = avatarName }, variationItems);
				});
				if (!nonFilter && !parentMatched) {
					avatarItems = avatarItems.Where(item => item.hasChildren);
				}

				return avatarItems.ToList();
			}

			List<TreeViewItem> BuildVariationTree(IEnumerable<AvatarVariation> variations, bool parentMatched) {
				IEnumerable<TreeViewItem> variationItems = variations.Select(variation => {
					string variationName = variation.DisplayNames.GetValueOrDefault(langKey, variation.VariationName);
					return new TreeViewItem(id++, new TreeItemData { name = variationName });
				});
				if (!nonFilter && !parentMatched) {
					variationItems = variationItems.Where(item => item.data.name.Contains(filter!));
				}

				return variationItems.ToList();
			}
		}


		private static VisualElement MakeItem() {
			return new Label {
				name = "label",
				enableRichText = true
			};
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

		internal struct TreeItemData {
			public string name;
		}

		internal new class UxmlFactory : UxmlFactory<AvatarTreeView, UxmlTraits> { }

		internal new class UxmlTraits : TreeView.UxmlTraits { }
	}
}
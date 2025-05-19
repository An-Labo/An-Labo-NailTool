using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Model;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.VisualElements {
	using TreeViewItem = TreeViewItemData<AvatarTreeView.TreeItemData>;
	internal class AvatarTreeView : TreeView {
		
		public void Init() {
			using DBShop _dbShop = new();
			int id = 0;
			string langKey = LanguageManager.CurrentLanguageData.language;
			List<TreeViewItem> rootItems = _dbShop.collection.Select(shop => {
					List<TreeViewItem> childItems = shop.Avatars
						.Select(pair => pair.Value)
						.Select(avatar => {
							List<TreeViewItem> childItems = avatar.AvatarVariations
								.Select(pair => pair.Value)
								.Select(variation => new TreeViewItem(id++, new TreeItemData { name = variation.DisplayNames.GetValueOrDefault(langKey, variation.VariationName) }))
								.ToList();
							return new TreeViewItem(id++, new TreeItemData { name = avatar.DisplayNames.GetValueOrDefault(langKey, avatar.AvatarName) }, childItems);
						})
						.ToList();
					return new TreeViewItem(id++, new TreeItemData { name = shop.DisplayNames.GetValueOrDefault(langKey, shop.ShopName) }, childItems);
				})
				.ToList();
			this.SetRootItems(rootItems);
			this.makeItem = MakeItem;
			this.bindItem = this.BindItem;
		}

		private static VisualElement MakeItem() {
			return new Label {
				name = "label"
			};
		}

		private void BindItem(VisualElement element, int index) {
			element.Q<Label>().text = this.GetItemDataForIndex<TreeItemData>(index).name;
		}

		internal struct TreeItemData {
			public string name;
		}

		internal new class UxmlFactory : UxmlFactory<AvatarTreeView, UxmlTraits> { }

		internal new class UxmlTraits : TreeView.UxmlTraits { }
	}
}
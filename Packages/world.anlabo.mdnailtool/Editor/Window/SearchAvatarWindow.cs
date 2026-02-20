using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.VisualElements;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Window {
	public class SearchAvatarWindow : EditorWindow {
		internal static void ShowWindow(MDNailToolWindow parentWindow) {
			SearchAvatarWindow? window = CreateInstance<SearchAvatarWindow>();
			window.titleContent = new GUIContent(LanguageManager.S("window.search_avatar"));
			window._parentWindow = parentWindow;
			window.ShowAuxWindow();
		}

		private const string GUID = "e80e099b4dadd704f9daf670bbc8b7b5";

		private MDNailToolWindow? _parentWindow;
		private AvatarTreeView _avatarTreeView = null!;

		private void CreateGUI() {
			// 共有USSを先に読み込む
			var uss = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(MDNailToolGuids.WindowUss);
			if (uss != null) this.rootVisualElement.styleSheets.Add(uss);

			string uxmlPath = AssetDatabase.GUIDToAssetPath(GUID);
			VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
			uxml.CloneTree(this.rootVisualElement);

			ToolbarSearchField searchField = this.rootVisualElement.Q<ToolbarSearchField>("search-field");
			searchField.RegisterValueChangedCallback(evt => this._avatarTreeView.SetFilter(evt.newValue));

			this._avatarTreeView = this.rootVisualElement.Q<AvatarTreeView>("avatar-tree-view");
			this._avatarTreeView.Init();
			this._avatarTreeView.selectionChanged += this.AvatarTreeViewOnSelectionChanged;
			this._avatarTreeView.itemsChosen += AvatarTreeViewOnChosen;
		}


		private void AvatarTreeViewOnSelectionChanged(IEnumerable<object> data) {
			AvatarTreeView.TreeItemData itemData = data.Cast<AvatarTreeView.TreeItemData>().First();
			this._parentWindow?.SetAvatar(itemData.shop, itemData.avatar, itemData.variation);
		}

		private void AvatarTreeViewOnChosen(IEnumerable<object> data) {
			this.AvatarTreeViewOnSelectionChanged(data);
			this.Close();
		}
	}
}
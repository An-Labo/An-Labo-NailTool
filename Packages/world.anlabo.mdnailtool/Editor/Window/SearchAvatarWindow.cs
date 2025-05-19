using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.VisualElements;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Window {
	public class SearchAvatarWindow : EditorWindow {
		[MenuItem("Test/Search Avatar Window")]
		private static void ShowTestWindow() {
			var window = GetWindow<SearchAvatarWindow>("Test");
			window.Show();
		}
		
		private const string GUID = "e80e099b4dadd704f9daf670bbc8b7b5";
		
		private AvatarTreeView _avatarTreeView = null!;

		private void CreateGUI() {
			string uxmlPath = AssetDatabase.GUIDToAssetPath(GUID);
			VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
			uxml.CloneTree(this.rootVisualElement);
			
			this._avatarTreeView = this.rootVisualElement.Q<AvatarTreeView>("avatar-tree-view");
			this._avatarTreeView.Init();
			
			ToolbarSearchField searchField = this.rootVisualElement.Q<ToolbarSearchField>("search-field");
			searchField.RegisterValueChangedCallback(evt => this._avatarTreeView.SetFilter(evt.newValue));
		}
	}
}
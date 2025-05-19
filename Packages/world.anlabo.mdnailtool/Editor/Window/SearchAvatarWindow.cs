using UnityEditor;
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

		private void CreateGUI() {
			string uxmlPath = AssetDatabase.GUIDToAssetPath(GUID);
			VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
			uxml.CloneTree(this.rootVisualElement);
			
			AvatarTreeView avatarTreeView = this.rootVisualElement.Q<AvatarTreeView>("AvatarTreeView");
			avatarTreeView.Init();
		}
	}
}
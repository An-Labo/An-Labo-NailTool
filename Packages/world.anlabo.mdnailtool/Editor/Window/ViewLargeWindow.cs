using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace world.anlabo.mdnailtool.Editor.Window {
	public class ViewLargeWindow : EditorWindow {
#if MD_NAIL_DEVELOP
		[MenuItem("An-Labo/Debug/DeleteWindow")]
		public static void A() {
			ViewLargeWindow window = GetWindow<ViewLargeWindow>();
			window.Close();
		}
#endif
		
		public static void ShowPopupWindow() {
			ViewLargeWindow window = CreateInstance<ViewLargeWindow>();
			window.ShowAsDropDown(Rect.zero, new Vector2(970, 950));
			
		}

		private const string GUID = "de53629f6700d0a4d816623a220fef98";
		private void CreateGUI() {
			var uxmlPath = MDNailToolAssetLoader.ResolveGuidToPath(GUID);
			VisualTreeAsset uxml = MDNailToolAssetLoader.LoadAssetSafe<VisualTreeAsset>(uxmlPath);
			uxml.CloneTree(this.rootVisualElement);
		}
	}
}
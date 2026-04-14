using UnityEditor;
using world.anlabo.mdnailtool.Editor.Window;

namespace world.anlabo.mdnailtool.Editor {
	public static class MenuActions {
		[MenuItem("An-Labo/An-Labo NailTool", false, 0)]
		private static void ShowMDNailToolWindow() {
			MDNailToolWindow.ShowWindow();
		}
	}
}
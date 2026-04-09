using UnityEditor;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Report;
using world.anlabo.mdnailtool.Editor.Window;

namespace world.anlabo.mdnailtool.Editor {
	public static class MenuActions {
		private const string MENU_ROOT = "An-Labo/";

		[MenuItem(MENU_ROOT + "An-Labo NailTool", false, 0)]
		private static void ShowMDNailToolWindow() {
			MDNailToolWindow.ShowWindow();
		}
		
		[MenuItem(MENU_ROOT + "Reload Design", false, 100)]
		private static void ReInstallLegacyDesign() {
			LegacyDesignInstaller.ReInstallLegacyNail();
		}
		
		[MenuItem(MENU_ROOT + "Report Generator", false, 101)]
		private static void ShowReportGeneratorWindow() {
			ReportGeneratorWindow.ShowWindow();
		}

		[MenuItem(MENU_ROOT + "Reload Languages", false, 102)]
		private static void ReloadLanguages() {
			LanguageManager.ReloadLanguages();
		}
	}
}
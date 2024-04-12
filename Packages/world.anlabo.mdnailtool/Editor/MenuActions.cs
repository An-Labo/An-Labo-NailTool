using UnityEditor;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Report;
using world.anlabo.mdnailtool.Editor.Window;

namespace world.anlabo.mdnailtool.Editor {
	public static class MenuActions {
		private const string MENU_ROOT = "An-Labo/";

		[MenuItem(MENU_ROOT + "MDNail Setup Tool", false, 0)]
		private static void ShowMDNailToolWindow() {
			MDNailToolWindow.ShowWindow();
		}


		[MenuItem(MENU_ROOT + "Report Generator", false, 1)]
		private static void ShowReportGeneratorWindow() {
			ReportGeneratorWindow.ShowWindow();
		}

		[MenuItem(MENU_ROOT + "Re Install Legacy Design", false, 12)]
		private static void ReInstallLegacyDesign() {
			LegacyDesignInstaller.ReInstallLegacyNail();
		}

		[MenuItem(MENU_ROOT + "Reload Languages", false, 13)]
		private static void ReloadLanguages() {
			LanguageManager.ReloadLanguages();
		}

		[MenuItem(MENU_ROOT + "Clear An-Labo NailTool's Global Setting", false, 14)]
		private static void ClearGlobalSetting() {
			GlobalSetting.ClearGlobalSettings();
		}
	}
}
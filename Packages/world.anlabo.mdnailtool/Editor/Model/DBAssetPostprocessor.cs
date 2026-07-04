using UnityEditor;

namespace world.anlabo.mdnailtool.Editor.Model {
	// DB json 変更を検知して該当キャッシュのみクリア. VCC 更新 / 外部編集 / Unity 内編集
	// いずれでも Unity の asset reimport が走った時点で発火.
	internal class DBAssetPostprocessor : AssetPostprocessor {
		private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
			string[] moved, string[] movedFrom) {
			foreach (string p in imported) {
				if (p == MDNailToolDefines.DB_NAIL_DESIGN_FILE_PATH) DBNailDesign.ClearCache();
				else if (p == MDNailToolDefines.DB_SHOP_FILE_PATH) DBShop.ClearCache();
				else if (p == MDNailToolDefines.DB_NAIL_SHAPE_FILE_PATH) DBNailShape.ClearCache();
				else if (p == MDNailToolDefines.DB_ADDITIONAL_ASSETS_FILE_PATH) DBAdditionalAssets.ClearCache();
			}
		}
	}
}

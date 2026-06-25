using System.IO;
using UnityEditor;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
    // Stage8: 旧版 (0.9.x 以前) でユーザー Assets 配下に展開された Resource キャッシュを VCC update 後に自動削除する.
    // 0.10.0 以降は Package/Resource/ 直参照のため Assets 側は不要.
    [InitializeOnLoad]
    public static class LegacyResourceCleanup {
        private const string CLEANUP_DONE_FLAG = "world.anlabo.mdnailtool.legacy_resource_cleanup_done";

        private static string LegacyResourcePath => MDNailToolDefines.ROOT_ASSET_PATH.TrimEnd('/') + "/Resource";

        static LegacyResourceCleanup() {
            EditorApplication.delayCall += TryCleanup;
        }

        private static void TryCleanup() {
            if (SessionState.GetBool(CLEANUP_DONE_FLAG, false)) return;
            SessionState.SetBool(CLEANUP_DONE_FLAG, true);

            string legacyResourcePath = LegacyResourcePath;
            if (!AssetDatabase.IsValidFolder(legacyResourcePath)) return;

            string full = Path.GetFullPath(legacyResourcePath);
            ToolConsole.Log($"[Stage8] 旧版 Assets 配下 Resource キャッシュを検出. 削除します: {full}");
            if (AssetDatabase.DeleteAsset(legacyResourcePath)) {
                ToolConsole.Log("[Stage8] 旧 Resource キャッシュ削除完了.");
                AssetDatabase.Refresh();
            } else {
                ToolConsole.Warn("[Stage8]", $"旧 Resource キャッシュ削除失敗: {legacyResourcePath}");
            }
        }
    }
}

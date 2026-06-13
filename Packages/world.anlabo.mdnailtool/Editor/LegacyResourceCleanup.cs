using System.IO;
using UnityEditor;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
    // Stage8: 旧版 (0.9.x 以前) でユーザー Assets 配下に展開された Resource キャッシュを VCC update 後に自動削除する.
    // 0.10.0 以降は Package/Resource/ 直参照のため Assets 側は不要.
    [InitializeOnLoad]
    public static class LegacyResourceCleanup {
        private const string LEGACY_RESOURCE_PATH = "Assets/[An-Labo.Virtual]/An-Labo Nail Tool/Resource";
        private const string CLEANUP_DONE_FLAG = "world.anlabo.mdnailtool.legacy_resource_cleanup_done";

        static LegacyResourceCleanup() {
            EditorApplication.delayCall += TryCleanup;
        }

        private static void TryCleanup() {
            if (SessionState.GetBool(CLEANUP_DONE_FLAG, false)) return;
            SessionState.SetBool(CLEANUP_DONE_FLAG, true);

            if (!AssetDatabase.IsValidFolder(LEGACY_RESOURCE_PATH)) return;

            string full = Path.GetFullPath(LEGACY_RESOURCE_PATH);
            ToolConsole.Log($"[Stage8] 旧版 Assets 配下 Resource キャッシュを検出. 削除します: {full}");
            if (AssetDatabase.DeleteAsset(LEGACY_RESOURCE_PATH)) {
                ToolConsole.Log("[Stage8] 旧 Resource キャッシュ削除完了.");
                AssetDatabase.Refresh();
            } else {
                ToolConsole.Warn("[Stage8]", $"旧 Resource キャッシュ削除失敗: {LEGACY_RESOURCE_PATH}");
            }
        }
    }
}

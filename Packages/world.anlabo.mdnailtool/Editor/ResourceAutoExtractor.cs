using UnityEditor;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
    // Stage8: 静的リソースを Package/Resource/ 直参照に統一. 旧 zip 展開機構は廃止し,
    // 既存呼び出しのコンパイル互換のため API シグネチャだけ no-op として残置.
    public static class ResourceAutoExtractor {
        public static void EnsureEssentialsExtractedSync() { }

        public static void EnsureDesignExtracted(string designName) { }

        public static void EnsurePrefabExtractedByGuid(string guid) { }

        public static string? TryResolvePrefabFromDiskMeta(string guid) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        public static void ResetResources(bool skipConfirmDialog = false) {
            // Stage8: 旧 Assets/Resource 展開キャッシュ廃止により reset 概念が消滅. no-op.
        }
    }
}

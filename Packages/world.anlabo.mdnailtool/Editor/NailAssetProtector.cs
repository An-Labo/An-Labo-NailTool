#nullable enable

using UnityEditor;
using UnityEngine;

namespace world.anlabo.mdnailtool.Editor
{
    // Projectウィンドウ上で非表示・編集不可にする(ファイル自体はdisk上に残る)
    internal class NailAssetProtector : AssetPostprocessor
    {
        private const string PackageRoot = "Packages/world.anlabo.mdnailtool/";

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (!path.StartsWith(PackageRoot)) continue;

                Object? asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;

                asset.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
            }
        }
    }
}

using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Model;
using Object = UnityEngine.Object;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public partial class NailSetupProcessor {
		// 一部の非ASCII文字を .meta が拒否するため、英数とかな・カナ・漢字のみ通す。それ以外は '_' 置換。
		private static string SanitizeForFileName(string name) {
			if (string.IsNullOrEmpty(name)) return "avatar";
			string sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_\-぀-ゟ゠-ヿ一-鿿]", "_");
			return string.IsNullOrEmpty(sanitized) ? "avatar" : sanitized;
		}

		public static void CreateBackup(GameObject avatarGameObject) {
			EnsureAssetFolderExists(MDNailToolDefines.BACKUP_PATH.TrimEnd('/'));

			GameObject clonedObject = Object.Instantiate(avatarGameObject);
			string safeAvatarName = SanitizeForFileName(avatarGameObject.name);
			string prefabName = $"bk_{safeAvatarName}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.prefab";

			try {
				PrefabUtility.SaveAsPrefabAsset(clonedObject, MDNailToolDefines.BACKUP_PATH + prefabName);
				AssetDatabase.Refresh();
			} finally {
				Object.DestroyImmediate(clonedObject);
			}
		}

		// AssetDatabase.CreateFolder で親階層も含めて .meta 同時生成する. Directory.CreateDirectory だと
		// 角括弧 / 空白入り path で .meta 連動が壊れ SaveAsPrefabAsset の .meta 書込が失敗する.
		private static void EnsureAssetFolderExists(string assetPath) {
			if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath)) return;
			string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "";
			string leaf = Path.GetFileName(assetPath);
			if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
			EnsureAssetFolderExists(parent);
			if (!AssetDatabase.IsValidFolder(assetPath)) AssetDatabase.CreateFolder(parent, leaf);
		}

		private string getPrefabPrefix() {
			Regex regex = new(@"(?<prefix>\[.+\]).+");
			Match match = regex.Match(this.NailPrefab.name);
			if (match.Success) return match.Groups["prefix"].Value;

				ToolConsole.Error("NailSetup", $"Failed to obtain nail prefix. ({this.NailPrefab?.name ?? "(null)"})");
			return "";
		}
	}
}

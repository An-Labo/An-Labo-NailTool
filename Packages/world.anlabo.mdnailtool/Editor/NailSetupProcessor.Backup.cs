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
			if (avatarGameObject == null) throw new ArgumentNullException(nameof(avatarGameObject));
			string safeAvatarName = SanitizeForFileName(avatarGameObject.name);
			string prefabName = $"bk_{safeAvatarName}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.prefab";
			CreateBackupAtPath(avatarGameObject, MDNailToolDefines.BACKUP_PATH + prefabName);
		}

		// Keep the default destination above; return the verified path for callers that need it.
		internal static string CreateBackupAtPath(GameObject avatarGameObject, string prefabPath) {
			if (avatarGameObject == null) throw new ArgumentNullException(nameof(avatarGameObject));
			if (string.IsNullOrEmpty(prefabPath)) throw new ArgumentException("A backup prefab path is required.", nameof(prefabPath));
			prefabPath = prefabPath.Replace('\\', '/');
			if (!prefabPath.StartsWith("Assets/", StringComparison.Ordinal)
				|| !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
				throw new ArgumentException("Backup must be a prefab under Assets.", nameof(prefabPath));
			foreach (string segment in prefabPath.Split('/')) {
				if (segment.Length == 0 || segment == "." || segment == "..")
					throw new ArgumentException("Invalid backup path.", nameof(prefabPath));
			}
			EnsureAssetFolderExists(Path.GetDirectoryName(prefabPath)!.Replace('\\', '/'));
			string uniquePath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
			if (string.IsNullOrEmpty(uniquePath) || File.Exists(uniquePath))
				throw new IOException($"Could not reserve a new backup path: {prefabPath}");

			GameObject clonedObject = Object.Instantiate(avatarGameObject);
			try {
				GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(clonedObject, uniquePath, out bool success);
				if (!success || savedPrefab == null || !File.Exists(uniquePath)
					|| string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(uniquePath)))
					throw new IOException($"Could not save the avatar backup: {uniquePath}");
				return uniquePath;
			} finally {
				if (clonedObject != null) Object.DestroyImmediate(clonedObject);
			}
		}

		// Create folders through AssetDatabase so their .meta files are created together.
		private static void EnsureAssetFolderExists(string assetPath) {
			if (AssetDatabase.IsValidFolder(assetPath)) return;
			string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "";
			string leaf = Path.GetFileName(assetPath);
			if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
				throw new IOException($"Could not access the backup folder: {assetPath}");
			EnsureAssetFolderExists(parent);
			if (!AssetDatabase.IsValidFolder(assetPath)) {
				string guid = AssetDatabase.CreateFolder(parent, leaf);
				if (string.IsNullOrEmpty(guid) || !AssetDatabase.IsValidFolder(assetPath))
					throw new IOException($"Could not create the backup folder: {assetPath}");
			}
		}

		private string getPrefabPrefix() {
			// `[Point]` 単独 (BuildFromNodes 出力の root 名) でも match させる. 末尾 `.+` だと [...] の後に文字必須で fail.
			Regex regex = new(@"(?<prefix>\[[^\]]+\])");
			Match match = regex.Match(this.NailPrefab.name);
			if (match.Success) return match.Groups["prefix"].Value;

				ToolConsole.Error("NailSetup", $"Failed to obtain nail prefix. ({this.NailPrefab?.name ?? "(null)"})");
			return "";
		}
	}
}

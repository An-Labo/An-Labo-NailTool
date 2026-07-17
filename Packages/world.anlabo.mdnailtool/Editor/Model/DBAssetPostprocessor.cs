using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.NailDesigns;

namespace world.anlabo.mdnailtool.Editor.Model {
	// DB json 変更を検知して該当キャッシュをクリアする。
	// VCC 更新では DB json 個別ではなく package.json / package 全体の更新として通知されることがあるため、
	// package version 差分でも DB json の再 import と静的キャッシュ破棄を行う。
	[InitializeOnLoad]
	internal class DBAssetPostprocessor : AssetPostprocessor {
		private const string PackageJsonPath = MDNailToolDefines.ROOT_PACKAGE_PATH + "package.json";
		private const string VersionPrefKeyPrefix = "world.anlabo.mdnailtool.dbRefreshVersion.";

		private static readonly string[] DbAssetPaths = {
			MDNailToolDefines.DB_NAIL_DESIGN_FILE_PATH,
			MDNailToolDefines.DB_SHOP_FILE_PATH,
			MDNailToolDefines.DB_NAIL_SHAPE_FILE_PATH,
			MDNailToolDefines.DB_ADDITIONAL_ASSETS_FILE_PATH,
		};

		private static bool _refreshScheduled;
		private static bool _forceRefreshScheduled;

		static DBAssetPostprocessor() {
			SchedulePackageRefresh(force: false);
		}

		private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
			string[] moved, string[] movedFrom) {
			foreach (string p in imported) {
				if (IsPackageJsonPath(p)) {
					SchedulePackageRefresh(force: true);
					continue;
				}

				ClearCacheForDbAsset(p);
			}

			foreach (string p in deleted) {
				if (IsDbAssetPath(p) || IsPackageJsonPath(p)) SchedulePackageRefresh(force: true);
			}

			foreach (string p in moved) {
				if (IsDbAssetPath(p) || IsPackageJsonPath(p)) SchedulePackageRefresh(force: true);
			}

			foreach (string p in movedFrom) {
				if (IsDbAssetPath(p) || IsPackageJsonPath(p)) SchedulePackageRefresh(force: true);
			}
		}

		internal static void ForceRefreshDbAssetsAndCaches() {
			ReimportDbAssets();
			ClearAllCaches();
			RememberCurrentPackageVersion();
		}

		private static void SchedulePackageRefresh(bool force) {
			_forceRefreshScheduled |= force;
			if (_refreshScheduled) return;

			_refreshScheduled = true;
			EditorApplication.delayCall += RunScheduledPackageRefresh;
		}

		private static void RunScheduledPackageRefresh() {
			_refreshScheduled = false;
			bool force = _forceRefreshScheduled;
			_forceRefreshScheduled = false;

			string currentVersion = ReadPackageVersionFromDisk();
			if (string.IsNullOrEmpty(currentVersion)) return;

			string prefKey = GetVersionPrefKey();
			string lastVersion = EditorPrefs.GetString(prefKey, string.Empty);
			if (!force && string.Equals(lastVersion, currentVersion, StringComparison.Ordinal)) return;

			ReimportDbAssets();
			ClearAllCaches();
			EditorPrefs.SetString(prefKey, currentVersion);
			Debug.Log($"[MDNailTool] DB cache refreshed for package version {currentVersion}.");
		}

		private static void ReimportDbAssets() {
			foreach (string path in DbAssetPaths) {
				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			}
		}

		private static void ClearCacheForDbAsset(string path) {
			if (path == MDNailToolDefines.DB_NAIL_DESIGN_FILE_PATH) DBNailDesign.ClearCache();
			else if (path == MDNailToolDefines.DB_SHOP_FILE_PATH) DBShop.ClearCache();
			else if (path == MDNailToolDefines.DB_NAIL_SHAPE_FILE_PATH) DBNailShape.ClearCache();
			else if (path == MDNailToolDefines.DB_ADDITIONAL_ASSETS_FILE_PATH) DBAdditionalAssets.ClearCache();
		}

		private static void ClearAllCaches() {
			DBNailDesign.ClearCache();
			DBShop.ClearCache();
			DBNailShape.ClearCache();
			DBAdditionalAssets.ClearCache();
			INailProcessor.ClearCreatedMaterialCash();
			INailProcessor.ClearPreviewMaterialCash();
			MDNailToolDefines.ClearResourcePathCache();
			MDNailToolAssetLoader.ClearCaseResolveCache();
		}

		private static bool IsDbAssetPath(string path) {
			foreach (string dbPath in DbAssetPaths) {
				if (string.Equals(path, dbPath, StringComparison.Ordinal)) return true;
			}
			return false;
		}

		private static bool IsPackageJsonPath(string path) {
			return string.Equals(path, PackageJsonPath, StringComparison.Ordinal);
		}

		private static void RememberCurrentPackageVersion() {
			string currentVersion = ReadPackageVersionFromDisk();
			if (!string.IsNullOrEmpty(currentVersion)) EditorPrefs.SetString(GetVersionPrefKey(), currentVersion);
		}

		private static string GetVersionPrefKey() {
			return VersionPrefKeyPrefix + Application.dataPath;
		}

		private static string ReadPackageVersionFromDisk() {
			try {
				UnityEditor.PackageManager.PackageInfo packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PackageJsonPath);
				if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.version)) return packageInfo.version;

				string projectPackageJsonPath = Path.Combine(Directory.GetCurrentDirectory(), PackageJsonPath.Replace('/', Path.DirectorySeparatorChar));
				string path = File.Exists(projectPackageJsonPath) ? projectPackageJsonPath : PackageJsonPath;
				if (!File.Exists(path)) return string.Empty;

				JObject package = JObject.Parse(File.ReadAllText(path));
				return package.GetValue("version")?.Value<string>() ?? string.Empty;
			} catch (Exception e) {
				Debug.LogWarning($"[MDNailTool] Failed to read package version for DB cache refresh: {e.Message}");
				return string.Empty;
			}
		}
	}
}

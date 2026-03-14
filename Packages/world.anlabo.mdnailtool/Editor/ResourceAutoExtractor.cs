using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
    [InitializeOnLoad]
    public static class ResourceAutoExtractor {
        
        private const string VERSION_FILE_NAME = ".resource_version";
        private const string ZIP_ARCHIVE_PATH = "Packages/world.anlabo.mdnailtool/ResourceArchive/resource.zip.bytes";
        private const string PACKAGE_RESOURCE_PATH = "Packages/world.anlabo.mdnailtool/Resource/";
        private const string ASSETS_RESOURCE_PATH = "Assets/[An-Labo.Virtual]/An-Labo Nail Tool/Resource/";
        
        private static readonly string[] ESSENTIAL_FOLDERS = {
            "DB/",
            "Lang/",
            "Nail/Thumbnails/",
            "Preview/",
            "uss/"
        };

        private static readonly string[] ADD_ONLY_FOLDERS = {
            "Nail/Thumbnails/"
        };
        
        private static readonly string[] ESSENTIAL_FILES = {
            "Lang/langs.json",
            "DB/nailDesign.json",
            "DB/shop.json"
        };
        
        private static string VersionFilePath => ASSETS_RESOURCE_PATH + VERSION_FILE_NAME;
        
        private static bool _isExtracting = false;
        private static string? _zipRealPath = null;
        
        static ResourceAutoExtractor() {
            EditorApplication.delayCall += CheckAndExtractEssentials;
        }

        private static bool HasEssentialFiles() {
            foreach (string file in ESSENTIAL_FILES) {
                string assetsPath = ASSETS_RESOURCE_PATH + file;
                string packagePath = PACKAGE_RESOURCE_PATH + file;
                
                if (File.Exists(assetsPath)) continue;
                
                string packageFullPath = Path.GetFullPath(packagePath);
                if (File.Exists(packageFullPath)) continue;
                
                return false;
            }
            return true;
        }

        private static void CheckAndExtractEssentials() {
            if (_isExtracting) return;

            string currentVersion = MDNailToolDefines.Version;
            string? installedVersion = GetInstalledVersion();

            bool versionMismatch = installedVersion != currentVersion;
            bool missingFiles = !HasEssentialFiles();

            if (!versionMismatch && !missingFiles) return;

            string? zipPath = GetZipRealPath();
            if (zipPath == null) return;

            StartEssentialExtraction(currentVersion);
        }

        public static void EnsureEssentialsExtractedSync() {
            if (_isExtracting) return;
            if (HasEssentialFiles()) return;
            
            string? zipPath = GetZipRealPath();
            if (zipPath == null) return;
            
            try {
                _isExtracting = true;
                ExtractFoldersFromZip(ESSENTIAL_FOLDERS);
                SaveInstalledVersion(MDNailToolDefines.Version);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                FixTextureImportSettings(ASSETS_RESOURCE_PATH);
            } catch (Exception e) {
                Debug.LogError($"[MDNailTool] 同期展開失敗: {e.Message}");
            } finally {
                _isExtracting = false;
            }
        }

        internal static string? GetZipRealPath() {
            if (_zipRealPath != null && File.Exists(_zipRealPath)) {
                return _zipRealPath;
            }
            
            var zipAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ZIP_ARCHIVE_PATH);
            if (zipAsset != null) {
                string assetPath = AssetDatabase.GetAssetPath(zipAsset);
                string fullPath = Path.GetFullPath(assetPath);
                if (File.Exists(fullPath)) {
                    _zipRealPath = fullPath;
                    return _zipRealPath;
                }
            }
            
            var zipObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ZIP_ARCHIVE_PATH);
            if (zipObj != null) {
                string assetPath = AssetDatabase.GetAssetPath(zipObj);
                string fullPath = Path.GetFullPath(assetPath);
                if (File.Exists(fullPath)) {
                    _zipRealPath = fullPath;
                    return _zipRealPath;
                }
            }
            
            string directPath = Path.GetFullPath(ZIP_ARCHIVE_PATH);
            if (File.Exists(directPath)) {
                _zipRealPath = directPath;
                return _zipRealPath;
            }
            
            string packageCachePath = Path.Combine("Library", "PackageCache");
            if (Directory.Exists(packageCachePath)) {
                foreach (string dir in Directory.GetDirectories(packageCachePath, "world.anlabo.mdnailtool*")) {
                    string zipInCache = Path.Combine(dir, "ResourceArchive", "resource.zip.bytes");
                    if (File.Exists(zipInCache)) {
                        _zipRealPath = zipInCache;
                        return _zipRealPath;
                    }
                }
            }
            
            return null;
        }

        private static async void StartEssentialExtraction(string targetVersion) {
            _isExtracting = true;

            int progressId = Progress.Start("MDNailTool リソース展開", "準備中...", Progress.Options.Managed);

            try {
                int copiedFiles = 0;
                
                await Task.Run(() => {
                    copiedFiles = ExtractFoldersFromZip(ESSENTIAL_FOLDERS);
                });
                
                Progress.Report(progressId, 0.95f, "インポート中...");
                SaveInstalledVersion(targetVersion);
                AssetDatabase.Refresh(ImportAssetOptions.Default);
                FixTextureImportSettings(ASSETS_RESOURCE_PATH);
                
                Progress.Finish(progressId);
                Debug.Log($"[MDNailTool] リソース展開完了 ({copiedFiles} files)");
                
                MDNailToolDefines.ClearResourcePathCache();
                
            } catch (Exception e) {
                Progress.Finish(progressId, Progress.Status.Failed);
                Debug.LogError($"[MDNailTool] リソース展開失敗: {e.Message}");
            } finally {
                _isExtracting = false;
            }
        }

        private static int ExtractFoldersFromZip(string[] folders) {
            string? zipPath = GetZipRealPath();
            if (zipPath == null) return 0;

            if (!Directory.Exists(ASSETS_RESOURCE_PATH)) {
                Directory.CreateDirectory(ASSETS_RESOURCE_PATH);
            }

            int copiedFiles = 0;
            
            HashSet<string> folderMetaFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders) {
                string trimmed = folder.TrimEnd('/');
                folderMetaFiles.Add(trimmed + ".meta");
                
                string[] parts = trimmed.Split('/');
                string parentPath = "";
                foreach (string part in parts) {
                    if (!string.IsNullOrEmpty(parentPath)) {
                        folderMetaFiles.Add(parentPath + ".meta");
                    }
                    parentPath = string.IsNullOrEmpty(parentPath) ? part : parentPath + "/" + part;
                }
            }

            using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
                foreach (ZipArchiveEntry entry in archive.Entries) {
                    bool shouldExtract = false;
                    
                    foreach (string folder in folders) {
                        if (entry.FullName.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) {
                            shouldExtract = true;
                            break;
                        }
                    }
                    
                    if (!shouldExtract && folderMetaFiles.Contains(entry.FullName)) {
                        shouldExtract = true;
                    }
                    
                    if (!shouldExtract) continue;
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    string destPath = ASSETS_RESOURCE_PATH + entry.FullName;
                    string? destDir = Path.GetDirectoryName(destPath);

                    if (destDir != null && !Directory.Exists(destDir)) {
                        Directory.CreateDirectory(destDir);
                    }

                    bool addOnly = false;
                    foreach (string addOnlyFolder in ADD_ONLY_FOLDERS) {
                        if (entry.FullName.StartsWith(addOnlyFolder, StringComparison.OrdinalIgnoreCase)) {
                            addOnly = true;
                            break;
                        }
                    }

                    if (addOnly && File.Exists(destPath)) {
                        continue;
                    }

                    entry.ExtractToFile(destPath, overwrite: true);
                    copiedFiles++;
                }
            }

            return copiedFiles;
        }

        public static void EnsureDesignExtracted(string designName) {
            string assetsDesignPath = ASSETS_RESOURCE_PATH + "Nail/Design/" + designName + "/";
            
            if (Directory.Exists(assetsDesignPath)) return;
            
            string packageDesignPath = Path.GetFullPath(PACKAGE_RESOURCE_PATH + "Nail/Design/" + designName + "/");
            if (Directory.Exists(packageDesignPath)) return;
            
            string? zipPath = GetZipRealPath();
            if (zipPath == null) return;

            string designPrefix = "Nail/Design/" + designName + "/";

            try {
                int copiedFiles = 0;
                
                HashSet<string> folderMetaFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                folderMetaFiles.Add("Nail.meta");
                folderMetaFiles.Add("Nail/Design.meta");
                folderMetaFiles.Add("Nail/Design/" + designName + ".meta");
                
                using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
                    foreach (ZipArchiveEntry entry in archive.Entries) {
                        bool shouldExtract = entry.FullName.StartsWith(designPrefix, StringComparison.OrdinalIgnoreCase);
                        
                        if (!shouldExtract && folderMetaFiles.Contains(entry.FullName)) {
                            shouldExtract = true;
                        }
                        
                        if (!shouldExtract) continue;
                        if (string.IsNullOrEmpty(entry.Name)) continue;

                        string destPath = ASSETS_RESOURCE_PATH + entry.FullName;
                        string? destDir = Path.GetDirectoryName(destPath);
                        
                        if (destDir != null && !Directory.Exists(destDir)) {
                            Directory.CreateDirectory(destDir);
                        }

                        entry.ExtractToFile(destPath, overwrite: true);
                        copiedFiles++;
                    }
                }

                if (copiedFiles > 0) {
                    AssetDatabase.Refresh(ImportAssetOptions.Default);
                    FixTextureImportSettings(assetsDesignPath);
                }
                
            } catch (Exception e) {
                Debug.LogError($"[MDNailTool] デザイン展開失敗 ({designName}): {e.Message}");
            }
        }

        public static void EnsurePrefabExtractedByGuid(string guid) {
            if (string.IsNullOrEmpty(guid)) return;
            
            string? zipPath = GetZipRealPath();
            if (zipPath == null) return;

            try {
                string? targetFolder = null;
                
                using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
                    foreach (ZipArchiveEntry entry in archive.Entries) {
                        if (!entry.FullName.StartsWith("Nail/Prefab/", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!entry.Name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                        
                        using (StreamReader reader = new StreamReader(entry.Open())) {
                            string content = reader.ReadToEnd();
                            if (content.Contains(guid)) {
                                string[] parts = entry.FullName.Split('/');
                                if (parts.Length >= 3) {
                                    targetFolder = parts[2];
                                }
                                break;
                            }
                        }
                    }
                }
                
                if (targetFolder != null) {
                    ExtractPrefabFolder(targetFolder);
                }
                
            } catch (Exception e) {
                Debug.LogError($"[MDNailTool] GUID検索失敗 ({guid}): {e.Message}");
            }
        }

        private static void ExtractPrefabFolder(string prefabFolderName) {
            string? zipPath = GetZipRealPath();
            if (zipPath == null) return;

            string assetsPrefabPath = ASSETS_RESOURCE_PATH + "Nail/Prefab/" + prefabFolderName + "/";
            string prefabPrefix = "Nail/Prefab/" + prefabFolderName + "/";

            try {
                int copiedFiles = 0;
                
                HashSet<string> folderMetaFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                folderMetaFiles.Add("Nail.meta");
                folderMetaFiles.Add("Nail/Prefab.meta");
                folderMetaFiles.Add("Nail/Prefab/" + prefabFolderName + ".meta");
                
                using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
                    foreach (ZipArchiveEntry entry in archive.Entries) {
                        bool shouldExtract = entry.FullName.StartsWith(prefabPrefix, StringComparison.OrdinalIgnoreCase);
                        
                        if (!shouldExtract && folderMetaFiles.Contains(entry.FullName)) {
                            shouldExtract = true;
                        }
                        
                        if (!shouldExtract) continue;
                        if (string.IsNullOrEmpty(entry.Name)) continue;

                        string destPath = ASSETS_RESOURCE_PATH + entry.FullName;
                        string? destDir = Path.GetDirectoryName(destPath);
                        
                        if (destDir != null && !Directory.Exists(destDir)) {
                            Directory.CreateDirectory(destDir);
                        }

                        entry.ExtractToFile(destPath, overwrite: true);
                        copiedFiles++;
                    }
                }

                if (copiedFiles > 0) {
                    AssetDatabase.Refresh(ImportAssetOptions.Default);
                    FixTextureImportSettings(assetsPrefabPath);
                }
                
            } catch (Exception e) {
                Debug.LogError($"[MDNailTool] Prefabフォルダ展開失敗 ({prefabFolderName}): {e.Message}");
            }
        }

        [MenuItem("An-Labo/Import All Resources")]
        public static void ForceExtractAll() {
            if (_isExtracting) {
                Debug.LogWarning("[MDNailTool] 既に展開中です");
                return;
            }
            
            string? zipPath = GetZipRealPath();
            if (zipPath == null) {
                Debug.LogError("[MDNailTool] ZIPファイルが見つかりません");
                return;
            }
            
            if (!EditorUtility.DisplayDialog(
                "Import All Resources",
                "全リソースを展開します。この処理には数分かかる場合があります。\nThis will extract all resources. This process may take a few minutes.\n\n続行しますか？ / Continue?",
                "OK",
                "Cancel"
            )) {
                return;
            }
            
            StartFullExtraction();
        }
        
        private static async void StartFullExtraction() {
            _isExtracting = true;

            int progressId = Progress.Start("MDNailTool 全リソース展開", "準備中...", Progress.Options.Managed);

            try {
                int copiedFiles = 0;
                
                await Task.Run(() => {
                    copiedFiles = ExtractAllFromZip();
                });
                
                Progress.Report(progressId, 0.95f, "インポート中...");
                SaveInstalledVersion(MDNailToolDefines.Version);
                AssetDatabase.Refresh(ImportAssetOptions.Default);
                FixTextureImportSettings(ASSETS_RESOURCE_PATH);
                
                Progress.Finish(progressId);
                Debug.Log($"[MDNailTool] 全リソース展開完了 ({copiedFiles} files)");
                
                MDNailToolDefines.ClearResourcePathCache();
                
            } catch (Exception e) {
                Progress.Finish(progressId, Progress.Status.Failed);
                Debug.LogError($"[MDNailTool] リソース展開失敗: {e.Message}");
            } finally {
                _isExtracting = false;
            }
        }

        private static int ExtractAllFromZip() {
            string? zipPath = GetZipRealPath();
            if (zipPath == null) {
                throw new FileNotFoundException("ZIPファイルが見つかりません");
            }

            if (!Directory.Exists(ASSETS_RESOURCE_PATH)) {
                Directory.CreateDirectory(ASSETS_RESOURCE_PATH);
            }

            int copiedFiles = 0;

            using (ZipArchive archive = ZipFile.OpenRead(zipPath)) {
                foreach (ZipArchiveEntry entry in archive.Entries) {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    string destPath = ASSETS_RESOURCE_PATH + entry.FullName;
                    string? destDir = Path.GetDirectoryName(destPath);
                    
                    if (destDir != null && !Directory.Exists(destDir)) {
                        Directory.CreateDirectory(destDir);
                    }

                    entry.ExtractToFile(destPath, overwrite: true);
                    copiedFiles++;
                }
            }

            return copiedFiles;
        }

        [MenuItem("An-Labo/Reset Resources")]
        public static void ResetResources() {
            if (_isExtracting) {
                Debug.LogWarning("[MDNailTool] 既に展開中です");
                return;
            }

            string? zipPath = GetZipRealPath();
            if (zipPath == null) {
                Debug.LogError("[MDNailTool] ZIPファイルが見つかりません");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Reset Resources",
                "Resourceフォルダを削除し、必須リソースを再展開します。\nThis will delete the Resource folder and re-extract essential resources.\n\n続行しますか？ / Continue?",
                "OK",
                "Cancel"
            )) {
                return;
            }

            string fullResourcePath = Path.GetFullPath(ASSETS_RESOURCE_PATH);
            if (Directory.Exists(fullResourcePath)) {
                Directory.Delete(fullResourcePath, true);

                string metaFile = fullResourcePath.TrimEnd('/', '\\') + ".meta";
                if (File.Exists(metaFile)) {
                    File.Delete(metaFile);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            StartEssentialExtraction(MDNailToolDefines.Version);
        }

        private static void FixTextureImportSettings(string folderPath) {
            if (!Directory.Exists(folderPath)) return;
            
            string[] textureExtensions = { "*.png", "*.jpg", "*.jpeg" };
            
            foreach (string ext in textureExtensions) {
                string[] files = Directory.GetFiles(folderPath, ext, SearchOption.AllDirectories);
                foreach (string file in files) {
                    string assetPath = file.Replace("\\", "/");
                    
                    TextureImporter? importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null) continue;
                    
                    if (importer.textureShape != TextureImporterShape.Texture2D) {
                        importer.textureShape = TextureImporterShape.Texture2D;
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        private static string? GetInstalledVersion() {
            if (!File.Exists(VersionFilePath)) return null;
            try {
                return File.ReadAllText(VersionFilePath).Trim();
            } catch {
                return null;
            }
        }

        /// <summary>
        /// GUIDがAssetDatabaseで解決できない場合、ディスク上のPrefabフォルダの
        /// .metaファイルからGUIDを検索してアセットパスを返す。
        /// </summary>
        public static string? TryResolvePrefabFromDiskMeta(string guid) {
            if (string.IsNullOrEmpty(guid)) return null;

            string[] searchRoots = {
                ASSETS_RESOURCE_PATH + "Nail/Prefab",
                PACKAGE_RESOURCE_PATH + "Nail/Prefab"
            };

            foreach (string root in searchRoots) {
                string fullRoot = Path.GetFullPath(root);
                if (!Directory.Exists(fullRoot)) continue;

                try {
                    foreach (string metaFile in Directory.EnumerateFiles(fullRoot, "*.prefab.meta", SearchOption.AllDirectories)) {
                        string content = File.ReadAllText(metaFile);
                        if (!content.Contains(guid)) continue;

                        string prefabFile = metaFile.Substring(0, metaFile.Length - 5); // .meta 除去
                        if (!File.Exists(prefabFile)) continue;

                        string assetPath = prefabFile.Replace("\\", "/");
                        int idx = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
                        if (idx < 0) idx = assetPath.IndexOf("Packages/", StringComparison.Ordinal);
                        if (idx >= 0) assetPath = assetPath.Substring(idx);

                        return assetPath;
                    }
                } catch (Exception e) {
                    Debug.LogWarning($"[MDNailTool] .meta検索エラー ({root}): {e.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// メインNailPrefabの所在フォルダを基に、同ディレクトリ内から
        /// 指定名パターンのPrefabを検索して返す（GUIDに依存しない最終フォールバック）。
        /// </summary>
        public static string? TryResolvePrefabByFolderSearch(string mainPrefabPath, string variantName) {
            if (string.IsNullOrEmpty(mainPrefabPath) || string.IsNullOrEmpty(variantName)) return null;

            // メインPrefabのフォルダを取得
            string? prefabDir = Path.GetDirectoryName(mainPrefabPath);
            if (string.IsNullOrEmpty(prefabDir)) return null;

            // メインPrefabからベースネームを抽出 (例: "[Oval]Julius" → "Julius")
            string mainFileName = Path.GetFileNameWithoutExtension(mainPrefabPath);
            System.Text.RegularExpressions.Match m =
                System.Text.RegularExpressions.Regex.Match(mainFileName, @"\[.+\](?<baseName>.+)");
            string baseName = m.Success ? m.Groups["baseName"].Value : mainFileName;

            // 同ディレクトリ内で [VariantName]BaseName.prefab を検索
            string candidate = Path.Combine(prefabDir, $"[{variantName}]{baseName}.prefab").Replace("\\", "/");
            if (File.Exists(candidate)) {
                return candidate;
            }

            // メインPrefabからシェイプ名を抽出 (例: "[Natural]Sio" → "Natural")
            var shapeMatch = System.Text.RegularExpressions.Regex.Match(mainFileName, @"\[(?<shape>.+)\].+");
            string shapeName = shapeMatch.Success ? shapeMatch.Groups["shape"].Value : "";

            string[] searchRoots = {
                ASSETS_RESOURCE_PATH + "Nail/Prefab",
                PACKAGE_RESOURCE_PATH + "Nail/Prefab"
            };

            // 全Prefabフォルダから [VariantName]BaseName.prefab を検索
            foreach (string root in searchRoots) {
                string fullRoot = Path.GetFullPath(root);
                if (!Directory.Exists(fullRoot)) continue;

                try {
                    foreach (string file in Directory.EnumerateFiles(fullRoot, "*.prefab", SearchOption.AllDirectories)) {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        if (!fileName.StartsWith($"[{variantName}]", StringComparison.OrdinalIgnoreCase)) continue;

                        string assetPath = file.Replace("\\", "/");
                        int idx = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
                        if (idx < 0) idx = assetPath.IndexOf("Packages/", StringComparison.Ordinal);
                        if (idx >= 0) assetPath = assetPath.Substring(idx);

                        string foundBase = fileName.Substring($"[{variantName}]".Length);
                        if (string.Equals(foundBase, baseName, StringComparison.OrdinalIgnoreCase)) {
                            return assetPath;
                        }
                    }
                } catch { /* skip */ }
            }

            // フォルダ名にバリアント名を含むフォルダから、同じシェイプのプレハブを検索
            // 例: variant="Heel_Feet", shape="Natural" → Sio_Heel_Feet/[Natural]Sio_Heel_Feet.prefab
            if (!string.IsNullOrEmpty(shapeName)) {
                foreach (string root in searchRoots) {
                    string fullRoot = Path.GetFullPath(root);
                    if (!Directory.Exists(fullRoot)) continue;

                    try {
                        foreach (string dir in Directory.EnumerateDirectories(fullRoot)) {
                            string dirName = Path.GetFileName(dir);
                            if (dirName.IndexOf(variantName, StringComparison.OrdinalIgnoreCase) < 0) continue;

                            foreach (string file in Directory.EnumerateFiles(dir, $"[{shapeName}]*.prefab")) {
                                string assetPath = file.Replace("\\", "/");
                                int idx = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
                                if (idx < 0) idx = assetPath.IndexOf("Packages/", StringComparison.Ordinal);
                                if (idx >= 0) assetPath = assetPath.Substring(idx);
                                return assetPath;
                            }
                        }
                    } catch { /* skip */ }
                }
            }

            return null;
        }

        private static void SaveInstalledVersion(string version) {
            try {
                string? dir = Path.GetDirectoryName(VersionFilePath);
                if (dir != null && !Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(VersionFilePath, version);
            } catch (Exception e) {
                Debug.LogWarning($"[MDNailTool] バージョン保存失敗: {e.Message}");
            }
        }
    }
}
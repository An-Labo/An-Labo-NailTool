using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

#nullable enable

namespace world.anlabo.mdnailtool.Editor
{
	internal static class MDNailToolAssetLoader
	{
		// AssetDatabase.LoadAssetAtPath が `[]` 含むパスで空を返す Won't Fix バグの回避用. 同型複数アセット非対応 (順序不定).
		internal static T? LoadAssetSafe<T>(string? path) where T : Object
		{
			if (string.IsNullOrEmpty(path)) return null;
			return AssetDatabase.LoadAllAssetsAtPath(path!).OfType<T>().FirstOrDefault();
		}

		internal static GameObject? LoadPrefabSafe(string? path)
		{
			if (string.IsNullOrEmpty(path)) return null;
			return AssetDatabase.LoadAllAssetsAtPath(path!).OfType<GameObject>()
				.FirstOrDefault(go => go.transform.parent == null);
		}

		internal static GameObject? LoadPrefabByGuid(string? guid, string? fallbackPath = null)
		{
			string? path = ResolveGuidToPath(guid, fallbackPath);
			return LoadPrefabSafe(path);
		}

		/// <summary>
		/// GUIDからアセットをロードする。GUID解決失敗時はパスフォールバックを試みる。
		/// </summary>
		internal static T? LoadByGuid<T>(string guid, string? fallbackPath = null) where T : Object
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (!string.IsNullOrEmpty(path))
			{
				T? asset = LoadAssetSafe<T>(path);
				if (asset != null) return asset;
			}

			// フォールバック1: 呼び出し元が提供した既知パス
			if (!string.IsNullOrEmpty(fallbackPath))
			{
				T? asset = LoadAssetSafe<T>(fallbackPath!);
				if (asset != null)
				{
					RegisterPathHint(guid, fallbackPath!);
					return asset;
				}
			}

			// フォールバック2: 登録済みパスヒントから探す
			if (_guidPathHints.TryGetValue(guid, out string? hintPath))
			{
				T? asset = LoadAssetSafe<T>(hintPath);
				if (asset != null) return asset;
			}

			return null;
		}

		/// <summary>
		/// GUIDからパスを解決する。GUID解決失敗時はパスフォールバック。
		/// </summary>
		internal static string? ResolveGuidToPath(string? guid, string? fallbackPath = null)
		{
			if (string.IsNullOrEmpty(guid)) return fallbackPath;

			string path = AssetDatabase.GUIDToAssetPath(guid!);
			if (!string.IsNullOrEmpty(path)) return path;

			// フォールバック1: 呼び出し元が提供した既知パス
			if (!string.IsNullOrEmpty(fallbackPath))
			{
				if (AssetDatabase.LoadMainAssetAtPath(fallbackPath!) != null)
				{
					RegisterPathHint(guid!, fallbackPath!);
					return fallbackPath;
				}
			}

			// フォールバック2: 登録済みパスヒント
			if (_guidPathHints.TryGetValue(guid!, out string? hintPath))
			{
				if (AssetDatabase.LoadMainAssetAtPath(hintPath) != null) return hintPath;
			}

			return null;
		}

		/// <summary>
		/// サムネイルGUIDからTexture2Dをロードする。GUID失敗時はデザイン名でパスフォールバック。
		/// </summary>
		internal static Texture2D? LoadThumbnail(string? thumbnailGuid, string designName)
		{
			if (!string.IsNullOrEmpty(thumbnailGuid))
			{
				string path = AssetDatabase.GUIDToAssetPath(thumbnailGuid);
				if (!string.IsNullOrEmpty(path))
				{
					Texture2D? tex = LoadAssetSafe<Texture2D>(path);
					if (tex != null) return tex;
				}
			}

			// パスフォールバック: デザイン名からサムネイルパスを推測
			string thumbnailDir = MDNailToolDefines.RESOURCE_PATH + "Nail/Thumbnails/";
			string[] extensions = { ".jpg", ".png", ".jpeg" };
			foreach (string ext in extensions)
			{
				string fallbackPath = thumbnailDir + designName + ext;
				Texture2D? tex = LoadAssetSafe<Texture2D>(fallbackPath);
				if (tex != null) return tex;
			}

			return null;
		}

		/// <summary>
		/// シェーダーGUIDからShaderをロードする。GUID失敗時はパスフォールバック→Shader.Find。
		/// </summary>
		internal static Shader? LoadShader(string guid, string? fallbackShaderName = null)
		{
			// GUID既知のシェーダーにはパスフォールバックを設定
			string? shaderFallbackPath = null;
			if (guid == MDNailToolDefines.PREVIEW_SHADER_GUID)
				shaderFallbackPath = MDNailToolDefines.RESOURCE_PATH + "Preview/preview.shader";
			else if (guid == MDNailToolDefines.GRAY_SHADER_GUID)
				shaderFallbackPath = MDNailToolDefines.RESOURCE_PATH + "Preview/gray.shader";

			Shader? shader = LoadByGuid<Shader>(guid, shaderFallbackPath);
			if (shader != null) return shader;

			if (!string.IsNullOrEmpty(fallbackShaderName))
			{
				shader = Shader.Find(fallbackShaderName);
				if (shader != null) return shader;
			}

			return Shader.Find("Hidden/Internal-Colored");
		}

		private static readonly Dictionary<string, string?> _caseResolveCache = new();
		private static readonly HashSet<string> _warnedPaths = new();

		private static bool? _isCaseSensitiveFS;

		/// <summary>
		/// 実行環境のファイルシステムが大小文字を区別するかを判定する。初回アクセス時にキャッシュする。
		/// </summary>
		private static bool IsCaseSensitiveFS
		{
			get
			{
				if (_isCaseSensitiveFS.HasValue) return _isCaseSensitiveFS.Value;
				_isCaseSensitiveFS = !Directory.Exists("ASSETS");
				return _isCaseSensitiveFS.Value;
			}
		}

		/// <summary>
		/// パスからアセットをロードする。ロード失敗時はファイル名の大小無視でフォールバック。
		/// </summary>
		internal static T? LoadByPathCaseInsensitive<T>(string assetPath) where T : Object
		{
			T? asset = LoadAssetSafe<T>(assetPath);
			if (asset != null) return asset;
			if (!IsCaseSensitiveFS) return null;

			string? resolved = ResolveCaseInsensitivePath(assetPath);
			if (resolved == null) return null;
			return LoadAssetSafe<T>(resolved);
		}

		/// <summary>
		/// ファイルの存在を確認する。確認失敗時はファイル名の大小無視でフォールバック。
		/// </summary>
		internal static bool FileExistsCaseInsensitive(string path)
		{
			if (File.Exists(path)) return true;
			if (!IsCaseSensitiveFS) return false;
			return ResolveCaseInsensitivePath(path) != null;
		}

		/// <summary>
		/// ディレクトリの存在を確認する。確認失敗時はフォルダ名の大小無視でフォールバック。
		/// </summary>
		internal static bool DirectoryExistsCaseInsensitive(string path)
		{
			if (Directory.Exists(path)) return true;
			if (!IsCaseSensitiveFS) return false;
			if (string.IsNullOrEmpty(path)) return false;
			string normalized = path.TrimEnd('/', '\\').Replace('\\', '/');
			int slash = normalized.LastIndexOf('/');
			if (slash < 0) return false;

			string parentDir = normalized.Substring(0, slash);
			string dirName = normalized.Substring(slash + 1);
			if (!Directory.Exists(parentDir)) return false;

			foreach (string sub in Directory.EnumerateDirectories(parentDir))
			{
				if (string.Equals(Path.GetFileName(sub), dirName, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		private static string? ResolveCaseInsensitivePath(string path)
		{
			if (string.IsNullOrEmpty(path)) return null;
			if (_caseResolveCache.TryGetValue(path, out string? cached)) return cached;

			string normalized = path.Replace('\\', '/');
			int slash = normalized.LastIndexOf('/');
			if (slash < 0) { _caseResolveCache[path] = null; return null; }

			string dir = normalized.Substring(0, slash);
			string fileName = normalized.Substring(slash + 1);
			if (!Directory.Exists(dir)) { _caseResolveCache[path] = null; return null; }

			foreach (string file in Directory.EnumerateFiles(dir))
			{
				string candidate = Path.GetFileName(file);
				if (string.Equals(candidate, fileName, StringComparison.OrdinalIgnoreCase))
				{
					string result = $"{dir}/{candidate}";
					_caseResolveCache[path] = result;
					if (_warnedPaths.Add(dir))
					{
						ToolConsole.Log($"[Warning] Case mismatch resolved in '{dir}' (e.g. '{fileName}' -> '{candidate}').");
					}
					return result;
				}
			}
			_caseResolveCache[path] = null;
			return null;
		}

		/// <summary>
		/// 大小無視パス解決の結果キャッシュをクリアする。
		/// </summary>
		internal static void ClearCaseResolveCache()
		{
			_caseResolveCache.Clear();
			_warnedPaths.Clear();
		}

		/// <summary>
		/// ファイル未検出時の診断情報を生成する。親ディレクトリの有無・ファイル総数・サンプル(最大5件)を含む。
		/// </summary>
		internal static string BuildMissingFileDiagnostics(string expectedPath)
		{
			if (string.IsNullOrEmpty(expectedPath)) return "[diag] empty path";
			string normalized = expectedPath.Replace('\\', '/');
			int slash = normalized.LastIndexOf('/');
			if (slash < 0) return "[diag] malformed path";

			string dir = normalized.Substring(0, slash);
			string fileName = normalized.Substring(slash + 1);

			if (!Directory.Exists(dir))
			{
				return $"[diag] parent dir NOT FOUND: {dir}";
			}

			string[] allFiles;
			try { allFiles = Directory.GetFiles(dir); }
			catch (Exception e) { return $"[diag] dir exists but enumerate failed: {e.Message}"; }

			int total = allFiles.Length;
			var sample = new List<string>();
			for (int i = 0; i < allFiles.Length && sample.Count < 5; i++)
			{
				string name = Path.GetFileName(allFiles[i]);
				if (!name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
					sample.Add(name);
			}

			return $"[diag] dir exists ({total} files), no case-insensitive match for '{fileName}'. Sample: {string.Join(" | ", sample)}";
		}

		// GUID → パスのフォールバックヒント登録用
		private static readonly Dictionary<string, string> _guidPathHints = new();

		/// <summary>
		/// GUID解決失敗時のフォールバックパスを登録する
		/// </summary>
		internal static void RegisterPathHint(string guid, string assetPath)
		{
			_guidPathHints[guid] = assetPath;
		}
	}
}

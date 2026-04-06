using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace world.anlabo.mdnailtool.Editor
{
	internal static class MDNailToolAssetLoader
	{
		/// <summary>
		/// GUIDからアセットをロードする。GUID解決失敗時はパスフォールバックを試みる。
		/// </summary>
		internal static T? LoadByGuid<T>(string guid) where T : Object
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (!string.IsNullOrEmpty(path))
			{
				T? asset = AssetDatabase.LoadAssetAtPath<T>(path);
				if (asset != null) return asset;
			}

			// フォールバック: 登録済みパスヒントから探す
			if (_guidPathHints.TryGetValue(guid, out string? hintPath))
			{
				T? asset = AssetDatabase.LoadAssetAtPath<T>(hintPath);
				if (asset != null) return asset;
			}

			return null;
		}

		/// <summary>
		/// GUIDからパスを解決する。GUID解決失敗時はパスフォールバック。
		/// </summary>
		internal static string? ResolveGuidToPath(string? guid)
		{
			if (string.IsNullOrEmpty(guid)) return null;

			string path = AssetDatabase.GUIDToAssetPath(guid!);
			if (!string.IsNullOrEmpty(path)) return path;

			// フォールバック: 登録済みパスヒント
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
					Texture2D? tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
					if (tex != null) return tex;
				}
			}

			// パスフォールバック: デザイン名からサムネイルパスを推測
			string thumbnailDir = MDNailToolDefines.RESOURCE_PATH + "Nail/Thumbnails/";
			string[] extensions = { ".jpg", ".png", ".jpeg" };
			foreach (string ext in extensions)
			{
				string fallbackPath = thumbnailDir + designName + ext;
				Texture2D? tex = AssetDatabase.LoadAssetAtPath<Texture2D>(fallbackPath);
				if (tex != null) return tex;
			}

			return null;
		}

		/// <summary>
		/// シェーダーGUIDからShaderをロードする。GUID失敗時はパスフォールバック→Shader.Find。
		/// </summary>
		internal static Shader? LoadShader(string guid, string? fallbackShaderName = null)
		{
			Shader? shader = LoadByGuid<Shader>(guid);
			if (shader != null) return shader;

			// Resource/Preview/ 内のシェーダーをパスで探す
			if (guid == MDNailToolDefines.PREVIEW_SHADER_GUID)
			{
				shader = AssetDatabase.LoadAssetAtPath<Shader>(MDNailToolDefines.RESOURCE_PATH + "Preview/preview.shader");
				if (shader != null) return shader;
			}
			else if (guid == MDNailToolDefines.GRAY_SHADER_GUID)
			{
				shader = AssetDatabase.LoadAssetAtPath<Shader>(MDNailToolDefines.RESOURCE_PATH + "Preview/gray.shader");
				if (shader != null) return shader;
			}

			if (!string.IsNullOrEmpty(fallbackShaderName))
			{
				shader = Shader.Find(fallbackShaderName);
				if (shader != null) return shader;
			}

			return Shader.Find("Hidden/Internal-Colored");
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

using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Entity;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Model
{
	/// <summary>
	/// additionalAssets.json を読み込むユーティリティ。
	/// DBBase を使わず単純にロードする（キャッシュ不要）。
	/// </summary>
	internal static class DBAdditionalAssets
	{
		private static AdditionalAssetRegistry? _cached;

		internal static AdditionalAssetRegistry Load()
		{
			if (_cached != null) return _cached;

			string path = MDNailToolDefines.DB_ADDITIONAL_ASSETS_FILE_PATH;
			TextAsset? textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
			if (textAsset == null)
			{
				// ファイルが無ければ空のレジストリを返す
				_cached = new AdditionalAssetRegistry();
				return _cached;
			}

			_cached = JsonConvert.DeserializeObject<AdditionalAssetRegistry>(textAsset.text)
			          ?? new AdditionalAssetRegistry();
			return _cached;
		}

		/// <summary>キャッシュをクリアする（JSON変更後に呼ぶ）</summary>
		internal static void ClearCache()
		{
			_cached = null;
		}
	}
}

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace world.anlabo.mdnailtool.Editor.Language {
	internal class LanguageData {
		[JsonProperty] public string language;
		[JsonProperty] public string guid;
		[JsonProperty] public string displayName;

		private JObject _jsonObj;
		private JObject _JsonObj {
			get {
				if (this._jsonObj != null) return this._jsonObj;
				TextAsset json = this.LoadJsonAsset();
				if (json == null) {
					this._jsonObj = new JObject();
					return this._jsonObj;
				}
				this._jsonObj = JObject.Parse(json.text);
				return this._jsonObj;
			}
		}

		private TextAsset LoadJsonAsset() {
			// 1. GUIDで解決を試みる
			string path = AssetDatabase.GUIDToAssetPath(this.guid);
			TextAsset json = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(path);
			if (json != null) return json;

			// 2. GUIDが解決できない場合、パスベースでフォールバック
			string langFileName = this.language + ".json";
			string langDir = MDNailToolDefines.LANG_FILE_PATH.Replace("langs.json", "");
			string langFilePath = langDir + langFileName;
			json = AssetDatabase.LoadAssetAtPath<TextAsset>(langFilePath);
			if (json != null) return json;

			// 3. リソース展開を試みてリトライ
			ResourceAutoExtractor.EnsureEssentialsExtractedSync();
			path = AssetDatabase.GUIDToAssetPath(this.guid);
			json = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(path);
			if (json != null) return json;

			json = AssetDatabase.LoadAssetAtPath<TextAsset>(langFilePath);
			return json;
		}

		public string Localized(string textId) {
			if (!this._JsonObj.ContainsKey(textId)) return null;
			return this._JsonObj[textId]?.Value<string>();
		}
	}
}

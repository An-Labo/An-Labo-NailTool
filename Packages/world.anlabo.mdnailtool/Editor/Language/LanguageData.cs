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
				TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(this.guid));
				this._jsonObj = JObject.Parse(json.text);
				return this._jsonObj;
			}
		}

		public string Localized(string textId) {
			if (!this._JsonObj.ContainsKey(textId)) return null;
			return this._JsonObj[textId]?.Value<string>();
		}
	}
}
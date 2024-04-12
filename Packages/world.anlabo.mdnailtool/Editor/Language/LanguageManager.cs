using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace world.anlabo.mdnailtool.Editor.Language {
	/// <summary>
	/// 言語マネージャー
	/// </summary>
	internal static class LanguageManager {

		private static List<LanguageData> _languageDataList;
		/// <summary>
		/// 読み込まれてる言語データリスト
		/// </summary>
		public static List<LanguageData> LanguageDataList {
			get {
				if (_languageDataList != null) return _languageDataList;
				ReloadLanguages();
				return _languageDataList;
			}
		}

		/// <summary>
		/// 現在設定されてる言語
		/// </summary>
		public static LanguageData CurrentLanguageData {
			get {
				string currentLang = GlobalSetting.Language;
				if (currentLang == null) return GetDefaultLanguage();
				
				LanguageData currentLangData = LanguageDataList.FirstOrDefault(data => data.language == currentLang);
				if (currentLangData != null) {
					return currentLangData;
				}
				
				return GetDefaultLanguage();
			}
		}

		internal static void ChangeLanguage(string language) {
			if (!LanguageDataList.Select(data => data.language).Contains(language)) {
				throw new InvalidOperationException($"It's a language that doesn't exist : {language}");
			}
			
			GlobalSetting.Language = language;

		}

		private static LanguageData GetDefaultLanguage() {
			return LanguageDataList[0];
		}

		/// <summary>
		/// 指定されたテキストIDからローカライズされたテキストを取得します。
		/// </summary>
		/// <param name="textId">テキストID</param>
		/// <returns>ローカライズされたテキスト</returns>
		internal static string S(string textId) {
			return CurrentLanguageData.Localized(textId) ?? textId;
		}

		/// <summary>
		/// 言語ファイルをリロードします。
		/// </summary>
		public static void ReloadLanguages() {
			TextAsset langs = AssetDatabase.LoadAssetAtPath<TextAsset>(MDNailToolDefines.LANG_FILE_PATH);
			string json = langs.text;
			_languageDataList = JsonConvert.DeserializeObject<List<LanguageData>>(json);
		}
	}
}
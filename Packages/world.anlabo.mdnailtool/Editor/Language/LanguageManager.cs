using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Language {
	/// <summary>
	/// 言語マネージャー
	/// </summary>
	internal static class LanguageManager {

		private static List<LanguageData>? _languageDataList;
		/// <summary>
		/// 読み込まれてる言語データリスト
		/// </summary>
		public static List<LanguageData> LanguageDataList {
			get {
				if (_languageDataList != null) return _languageDataList;
				_languageDataList = LoadLanguages();
				return _languageDataList;
			}
		}

		/// <summary>
		/// 現在設定されてる言語
		/// </summary>
		public static LanguageData CurrentLanguageData {
			get {
				string? currentLang = GlobalSetting.Language;
				if (currentLang == null) return GetDefaultLanguage();
				
				LanguageData? currentLangData = LanguageDataList.FirstOrDefault(data => data.language == currentLang);
				if (currentLangData != null) {
					return currentLangData;
				}
				
				return GetDefaultLanguage();
			}
		}

		/// <summary>
		/// 言語データのキャッシュをクリアし、開いているNailToolウィンドウを再描画します。
		/// </summary>
		internal static void ReloadLanguages() {
			if (_languageDataList != null) {
				foreach (var lang in _languageDataList) {
					lang.ClearCache();
				}
			}
			_languageDataList = null;

			AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

			var windows = UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>();
			foreach (var w in windows) {
				if (w.GetType().FullName?.Contains("MDNailTool") == true) {
					w.rootVisualElement?.Clear();
					var createGui = w.GetType().GetMethod("CreateGUI",
						System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
					createGui?.Invoke(w, null);
				}
			}

			Debug.Log("[MDNailTool] 言語データをリロードしました");
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
		private static bool _hasRetried = false;

		/// <summary>
		/// 指定されたテキストIDからローカライズされたテキストを取得します。
		/// 見つからない場合はnullを返します。
		/// </summary>
		internal static string? S(string textId) {
			string? result = CurrentLanguageData.Localized(textId);
			if (result != null) return result;

			// 初回のみリトライ(リソース展開前にUIが描画された場合のフォールバック)
			if (!_hasRetried) {
				_hasRetried = true;
				_languageDataList?.ForEach(lang => lang.ClearCache());
				_languageDataList = null;
				result = CurrentLanguageData.Localized(textId);
			}

			return result;
		}

		/// <summary>
		/// 言語ファイルをリロードします。
		/// </summary>
		private static List<LanguageData> LoadLanguages() {
			TextAsset langs = AssetDatabase.LoadAssetAtPath<TextAsset>(MDNailToolDefines.LANG_FILE_PATH);
			string json = langs.text;
			return JsonConvert.DeserializeObject<List<LanguageData>>(json) ?? throw new InvalidOperationException("Not found language file.");
		}
	}
}
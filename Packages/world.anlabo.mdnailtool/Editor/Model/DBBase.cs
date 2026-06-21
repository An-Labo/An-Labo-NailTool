using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

#if DEBUG_MD_NAIL_DB
using System.Diagnostics;
using Debug = UnityEngine.Debug;
#endif

#nullable enable


namespace world.anlabo.mdnailtool.Editor.Model {
	public abstract class DBBase<T> : IDisposable {

		private static Dictionary<string, T>? _cash;
		// ReSharper disable once StaticMemberInGenericType
		private static uint _accessCount;
		
		public readonly IReadOnlyDictionary<string, T> dictionary;
		public readonly IReadOnlyCollection<T> collection;
		
		protected readonly Dictionary<string, T> _data;

		protected DBBase(string dbFilePath) {
			if (_cash != null) {
				this._data = _cash;
			} else {
				TextAsset? textAsset = MDNailToolAssetLoader.LoadAssetSafe<TextAsset>(dbFilePath);
				if (textAsset == null) throw new NailToolResourceException("DB", $"Not found DB : {dbFilePath}");
				Dictionary<string, T>? data = this.DeserializeRoot(textAsset.text);
				_cash = data ?? throw new NailToolResourceException("DB", $"Not found DB : {dbFilePath}");
				this._data = _cash;
			}
			this.dictionary = this._data;
			this.collection = this._data.Values;
			_accessCount++;
			#if DEBUG_MD_NAIL_DB
			Debug.Log($"DBConnect : {this.GetType().Name}, {_accessCount} : {new StackFrame(2, false).GetMethod()!.DeclaringType!.FullName}");
			#endif
			
		}

		~DBBase() {
			this.Dispose();
		}

		// json text を Dictionary<string, T> に展開するフック. 派生で root が非 Dictionary な json を扱える.
		protected virtual Dictionary<string, T>? DeserializeRoot(string jsonText) {
			return JsonConvert.DeserializeObject<Dictionary<string, T>>(jsonText);
		}

		/// <summary>
		/// 型パラメータ T ごとの静的キャッシュと参照カウントをリセットする。
		/// DB json ファイルを差し替えた直後に呼ぶ (次の new で再読込される)。
		/// 使用中のインスタンスが残っていても、Dispose 時の既存ゼロ化ロジックで整合。
		/// </summary>
		public static void ClearCache() {
			_cash = null;
			_accessCount = 0;
		}

		public void Dispose() {
			if (_accessCount > 0) _accessCount--;
#if DEBUG_MD_NAIL_DB
			Debug.Log($"DBDisconnect : {this.GetType().Name}, {_accessCount} : {new StackFrame(1, false).GetMethod()!.DeclaringType!.FullName}");
#endif
			GC.SuppressFinalize(this);
		}
	}
}
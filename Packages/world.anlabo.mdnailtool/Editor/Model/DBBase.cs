// #define DEBUG_MD_NAIL_DB

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
				TextAsset? textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(dbFilePath);
				Dictionary<string, T>? data = JsonConvert.DeserializeObject<Dictionary<string, T>>(textAsset.text);
				this._data = data ?? throw new FileNotFoundException($"Not found DB : {dbFilePath}");
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

		public void Dispose() {
			_accessCount--;
#if DEBUG_MD_NAIL_DB
			Debug.Log($"DBDisconnect : {this.GetType().Name}, {_accessCount} : {new StackFrame(1, false).GetMethod()!.DeclaringType!.FullName}");
#endif
			if (_accessCount <= 0) {
				_accessCount = 0;
				_cash = null;
#if DEBUG_MD_NAIL_DB
				Debug.Log($"DBClear : {this.GetType().Name}");
#endif
			}
			GC.SuppressFinalize(this);
		}
	}
}
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	/// <summary>
	/// Unity全体で共通の設定
	/// </summary>
	internal static class GlobalSetting {
		private const string LANGUAGE_KEY = "world.anlabo.mdnailtool.language";
		
		/// <summary>
		/// 言語設定
		/// </summary>
		internal static string? Language {
			get {
				if (!EditorPrefs.HasKey(LANGUAGE_KEY)) return null;
				return EditorPrefs.GetString(LANGUAGE_KEY);
			}
			set => EditorPrefs.SetString(LANGUAGE_KEY, value);
		}

		private const string LAST_USE_SHAPE_NAME_KEY = "world.anlabo.mdnailtool.last_use_shape_name";

		internal static string LastUseShapeName {
			get {
				if (!EditorPrefs.HasKey(LAST_USE_SHAPE_NAME_KEY)) return "Oval";
				return EditorPrefs.GetString(LAST_USE_SHAPE_NAME_KEY);
			}
			set => EditorPrefs.SetString(LAST_USE_SHAPE_NAME_KEY, value);
		}

		private const string USE_FOOT_NAIL_KEY = "world.anlabo.mdnailtool.use_foot_nail";
		internal static bool UseFootNail {
			get {
				if (!EditorPrefs.HasKey(USE_FOOT_NAIL_KEY)) return false;
				return EditorPrefs.GetBool(USE_FOOT_NAIL_KEY);
			}
			set => EditorPrefs.SetBool(USE_FOOT_NAIL_KEY, value);
		}

		private const string REMOVE_CURRENT_NAIL_KEY = "world.anlabo.mdnailtool.remove_current_nail";

		internal static bool RemoveCurrentNail {
			get {
				if (!EditorPrefs.HasKey(REMOVE_CURRENT_NAIL_KEY)) return true;
				return EditorPrefs.GetBool(REMOVE_CURRENT_NAIL_KEY);
			}
			set => EditorPrefs.SetBool(REMOVE_CURRENT_NAIL_KEY, value);
		}

		private const string GENERATE_MATERIAL_KEY = "world.anlabo.mdnailtool.generate_material";
		
		internal static bool GenerateMaterial {
			get {
				if (!EditorPrefs.HasKey(GENERATE_MATERIAL_KEY)) return true;
				return EditorPrefs.GetBool(GENERATE_MATERIAL_KEY);
			}
			set => EditorPrefs.SetBool(GENERATE_MATERIAL_KEY, value);
		}

		private const string BACKUP_KEY = "world.anlabo.mdnailtool.backup";
		
		internal static bool Backup {
			get {
				if (!EditorPrefs.HasKey(BACKUP_KEY)) return true;
				return EditorPrefs.GetBool(BACKUP_KEY);
			}
			set => EditorPrefs.SetBool(BACKUP_KEY, value);
		}
		
		private const string USE_MODULAR_AVATAR_KEY = "world.anlabo.mdnailtool.use_modular_avatar";
		internal static bool UseModularAvatar {
			get {
				if (!EditorPrefs.HasKey(USE_MODULAR_AVATAR_KEY)) return false;
				return EditorPrefs.GetBool(USE_MODULAR_AVATAR_KEY);
			}
			set => EditorPrefs.SetBool(USE_MODULAR_AVATAR_KEY, value);
		}

		private const string DESIGN_LAST_USED_TIMES_KEY = "world.anlabo.mdnailtool.design_last_used_times";
		internal static Dictionary<string, DateTime> DesignLastUsedTimes {
			get {
				if (!EditorPrefs.HasKey(DESIGN_LAST_USED_TIMES_KEY)) return new Dictionary<string, DateTime>();
				string json = EditorPrefs.GetString(DESIGN_LAST_USED_TIMES_KEY);
				Dictionary<string, DateTime>? obj = JsonConvert.DeserializeObject<Dictionary<string, DateTime>>(json);
				return obj ?? new Dictionary<string, DateTime>();
			}

			set {
				string json = JsonConvert.SerializeObject(value);
				EditorPrefs.SetString(DESIGN_LAST_USED_TIMES_KEY, json);
			}
		}

		private const string DESIGN_USE_COUNT_KEY = "world.anlabo.mdnailtool.design_use_count";
		internal static Dictionary<string, int> DesignUseCount {
			get {
				if (!EditorPrefs.HasKey(DESIGN_USE_COUNT_KEY)) return new Dictionary<string, int>();
				string json = EditorPrefs.GetString(DESIGN_USE_COUNT_KEY);
				Dictionary<string, int>? obj = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
				return obj ?? new Dictionary<string, int>();
			}

			set {
				string json = JsonConvert.SerializeObject(value);
				EditorPrefs.SetString(DESIGN_LAST_USED_TIMES_KEY, json);
			}
		}

		public static void ClearGlobalSettings() {
			EditorPrefs.DeleteKey(LANGUAGE_KEY);
			EditorPrefs.DeleteKey(LAST_USE_SHAPE_NAME_KEY);
			EditorPrefs.DeleteKey(USE_FOOT_NAIL_KEY);
			EditorPrefs.DeleteKey(REMOVE_CURRENT_NAIL_KEY);
			EditorPrefs.DeleteKey(BACKUP_KEY);
			EditorPrefs.DeleteKey(USE_MODULAR_AVATAR_KEY);
			EditorPrefs.DeleteKey(DESIGN_LAST_USED_TIMES_KEY);
		}

	}
}
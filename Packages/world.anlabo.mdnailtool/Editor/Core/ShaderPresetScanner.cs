#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace world.anlabo.mdnailtool.Editor.Core
{
	public static class ShaderPresetScanner
	{
		public const string USER_PREFIX = "User: ";

		private const string HIDDEN_PRESETS_KEY = "world.anlabo.mdnailtool.hidden_shader_presets";

		private static List<string>? _cachedNames;
		private static Dictionary<string, Material>? _cachedNameToMat;

		public static List<string> ScanAllPresetNames()
		{
			EnsureCache();
			HashSet<string> hidden = LoadHiddenSet();
			List<string> result = new();
			foreach (string name in _cachedNames!) {
				if (hidden.Contains(name)) continue;
				result.Add(name);
			}
			return result;
		}

		public static List<string> ScanAllPresetNamesIncludingHidden()
		{
			EnsureCache();
			return new List<string>(_cachedNames!);
		}

		public static Material? FindPresetByName(string presetName)
		{
			if (string.IsNullOrEmpty(presetName)) return null;
			EnsureCache();
			return _cachedNameToMat!.TryGetValue(presetName, out Material? mat) ? mat : null;
		}

		public static bool IsHidden(string presetName)
		{
			return LoadHiddenSet().Contains(presetName);
		}

		public static void SetHidden(string presetName, bool hidden)
		{
			HashSet<string> set = LoadHiddenSet();
			bool changed;
			if (hidden) {
				changed = set.Add(presetName);
			} else {
				changed = set.Remove(presetName);
			}
			if (changed) SaveHiddenSet(set);
		}

		public static void InvalidateCache()
		{
			_cachedNames = null;
			_cachedNameToMat = null;
		}

		private static void EnsureCache()
		{
			if (_cachedNames != null && _cachedNameToMat != null) return;

			_cachedNames = new List<string>();
			_cachedNameToMat = new Dictionary<string, Material>();

			foreach (Material mat in ScanFolder(MDNailToolDefines.SHADER_PRESET_BUILTIN_PATH))
			{
				if (_cachedNameToMat.ContainsKey(mat.name)) continue;
				_cachedNames.Add(mat.name);
				_cachedNameToMat[mat.name] = mat;
			}
			foreach (Material mat in ScanFolder(MDNailToolDefines.SHADER_PRESET_USER_PATH))
			{
				string key = USER_PREFIX + mat.name;
				if (_cachedNameToMat.ContainsKey(key)) continue;
				_cachedNames.Add(key);
				_cachedNameToMat[key] = mat;
			}
		}

		private static IEnumerable<Material> ScanFolder(string folderPath)
		{
			if (!AssetDatabase.IsValidFolder(folderPath)) yield break;

			string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				Material? mat = AssetDatabase.LoadAssetAtPath<Material>(path);
				if (mat != null) yield return mat;
			}
		}

		private static HashSet<string> LoadHiddenSet()
		{
			HashSet<string> set = new();
			string raw = EditorPrefs.GetString(HIDDEN_PRESETS_KEY, string.Empty);
			if (string.IsNullOrEmpty(raw)) return set;
			foreach (string s in raw.Split('\n')) {
				if (!string.IsNullOrEmpty(s)) set.Add(s);
			}
			return set;
		}

		private static void SaveHiddenSet(HashSet<string> set)
		{
			EditorPrefs.SetString(HIDDEN_PRESETS_KEY, string.Join("\n", set));
		}
	}
}

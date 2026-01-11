using UnityEditor;
using UnityEngine;

namespace world.anlabo.mdnailtool.Editor
{
	internal static class MDNailToolAssetLoader
	{
		internal static T LoadByGuid<T>(string guid) where T : Object
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(path))
			{
				Debug.LogError($"[MDNailTool] Asset not found. GUID={guid}");
				return null;
			}
			return AssetDatabase.LoadAssetAtPath<T>(path);
		}
	}
}

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;

namespace world.anlabo.mdnailtool.Editor
{
	public static partial class NailSetupUtil
	{
		public enum ShrinkBSScope
		{
			All,
			LeftOnly,
			RightOnly,
		}

		// パスに半角ブラケット `[]` が含まれると AssetDatabase.LoadAssetAtPath が
		// ワイルドカードと解釈する Unity Won't Fix バグ回避のため LoadAllAssetsAtPath を使う.
		// LoadAllAssetsAtPath はprefab内の全GameObject(root+子)を順序不定で返すので、
		// transform.parent == null で root を確実に取る (子オブジェクトを誤って拾う事故防止).
		public static GameObject? LoadPrefabAtPath(string? assetPath)
		{
			return MDNailToolAssetLoader.LoadPrefabSafe(assetPath);
		}

	}
}

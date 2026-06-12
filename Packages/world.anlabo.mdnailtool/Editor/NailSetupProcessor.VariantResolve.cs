using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using world.anlabo.mdnailtool.Editor.Entity;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public partial class NailSetupProcessor {
		// アバターのPrefab/FBXのGUIDからアセットフォルダを特定する
		private string? GetAvatarAssetFolder()
		{
			if (this.AvatarVariationData.AvatarPrefabs != null)
			{
				foreach (AvatarPrefab ap in this.AvatarVariationData.AvatarPrefabs)
				{
					if (string.IsNullOrEmpty(ap.PrefabGUID)) continue;
					string? path = MDNailToolAssetLoader.ResolveGuidToPath(ap.PrefabGUID);
					if (!string.IsNullOrEmpty(path))
						return Path.GetDirectoryName(path)?.Replace("\\", "/");
				}
			}
			if (this.AvatarVariationData.AvatarFbxs != null)
			{
				foreach (AvatarFbx fbx in this.AvatarVariationData.AvatarFbxs)
				{
					if (string.IsNullOrEmpty(fbx.FbxGUID)) continue;
					string? path = MDNailToolAssetLoader.ResolveGuidToPath(fbx.FbxGUID);
					if (!string.IsNullOrEmpty(path))
						return Path.GetDirectoryName(path)?.Replace("\\", "/");
				}
			}
			return null;
		}

		// バリアントのパスを GUID 優先 -> 抽出 retry -> ファイル名検索 の順で解決する
		private string? ResolveVariantPath(AvatarBlendShapeVariant variant)
		{
			string? variantPath = MDNailToolAssetLoader.ResolveGuidToPath(variant.NailPrefabGUID);
			if (string.IsNullOrEmpty(variantPath) || NailSetupUtil.LoadPrefabAtPath(variantPath) == null)
			{
				ResourceAutoExtractor.EnsurePrefabExtractedByGuid(variant.NailPrefabGUID);
				AssetDatabase.Refresh();
				variantPath = MDNailToolAssetLoader.ResolveGuidToPath(variant.NailPrefabGUID);
			}
			if (!string.IsNullOrEmpty(variantPath) && NailSetupUtil.LoadPrefabAtPath(variantPath) == null)
			{
				AssetDatabase.ImportAsset(variantPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
			}
			if (string.IsNullOrEmpty(variantPath) || NailSetupUtil.LoadPrefabAtPath(variantPath) == null)
			{
				string? diskPath = ResourceAutoExtractor.TryResolvePrefabFromDiskMeta(variant.NailPrefabGUID);
				if (!string.IsNullOrEmpty(diskPath))
				{
					AssetDatabase.ImportAsset(diskPath!, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
					variantPath = diskPath!;
				}
			}
			if (string.IsNullOrEmpty(variantPath) || NailSetupUtil.LoadPrefabAtPath(variantPath) == null)
			{
				string? found = FindVariantPrefabByName(variant.Name);
				if (!string.IsNullOrEmpty(found))
				{
#if MD_NAIL_DEVELOP
					ToolConsole.Log($"Variant '{variant.Name}': ファイル名から検出 -> {found}");
#endif
					variantPath = found!;
				}
			}
			if (string.IsNullOrEmpty(variantPath) || NailSetupUtil.LoadPrefabAtPath(variantPath) == null)
				return null;
			return variantPath;
		}

		// Prefabフォルダ内で [ShapeName]VariantName.prefab を検索する
		private string? FindVariantPrefabByName(string variantName)
		{
			string mainPrefabPath = AssetDatabase.GetAssetPath(this.NailPrefab);
			string mainFileName = Path.GetFileNameWithoutExtension(mainPrefabPath);
			var shapeMatch = Regex.Match(mainFileName, @"\[(?<shape>.+)\].+");
			string shapeName = shapeMatch.Success ? shapeMatch.Groups["shape"].Value : "Natural";

			string expectedFileName = $"[{shapeName}]{variantName}.prefab";

			string[] searchRoots = {
				"Assets/[An-Labo.Virtual]/An-Labo Nail Tool/Resource/Nail/Prefab",
				"Packages/world.anlabo.mdnailtool/Nail/Prefab"
			};

			string? avatarFolder = GetAvatarAssetFolder();
			List<string> allRoots = new(searchRoots);
			if (!string.IsNullOrEmpty(avatarFolder)) allRoots.Add(avatarFolder!);

			foreach (string root in allRoots)
			{
				string fullRoot = Path.GetFullPath(root);
				if (!Directory.Exists(fullRoot)) continue;

				try
				{
					foreach (string file in Directory.EnumerateFiles(fullRoot, expectedFileName, SearchOption.AllDirectories))
					{
						string assetPath = file.Replace("\\", "/");
						int idx = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
						if (idx >= 0) assetPath = assetPath.Substring(idx);
						return assetPath;
					}
				}
				catch { /* skip */ }
			}

			return null;
		}
	}
}

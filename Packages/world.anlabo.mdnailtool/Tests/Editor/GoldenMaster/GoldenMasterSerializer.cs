using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Tests.GoldenMaster
{
	internal static class GoldenMasterSerializer
	{
		internal class Snapshot
		{
			public string CaseName = "";
			public List<NodeRecord> Nodes = new();
			public List<BlendShapeRecord> BlendShapes = new();
			public List<string> Warnings = new();
			public List<string> Errors = new();
			public string? ProcessException;
		}

		internal class NodeRecord
		{
			public string Path = "";
			public string[] ComponentTypes = System.Array.Empty<string>();
			public string? MeshGuid;
			public string[] MaterialGuids = System.Array.Empty<string>();
		}

		internal class BlendShapeRecord
		{
			public string SmrPath = "";
			public string BlendShapeName = "";
			public float Weight;
		}

		internal static Snapshot Capture(Transform root, string caseName)
		{
			Snapshot snap = new() { CaseName = caseName };

			foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
			{
				string path = GetRelativePath(root, t);
				NodeRecord node = new() { Path = path };

				List<string> typeNames = new();
				foreach (Component c in t.GetComponents<Component>())
				{
					if (c == null) continue;
					typeNames.Add(c.GetType().FullName ?? c.GetType().Name);
				}
				typeNames.Sort();
				node.ComponentTypes = typeNames.ToArray();

				if (t.TryGetComponent(out SkinnedMeshRenderer smr))
				{
					node.MeshGuid = ToGuid(smr.sharedMesh);
					node.MaterialGuids = ToGuids(smr.sharedMaterials);

					for (int i = 0; smr.sharedMesh != null && i < smr.sharedMesh.blendShapeCount; i++)
					{
						snap.BlendShapes.Add(new BlendShapeRecord
						{
							SmrPath = path,
							BlendShapeName = smr.sharedMesh.GetBlendShapeName(i),
							Weight = smr.GetBlendShapeWeight(i),
						});
					}
				}
				else if (t.TryGetComponent(out MeshRenderer mr))
				{
					if (t.TryGetComponent(out MeshFilter mf)) node.MeshGuid = ToGuid(mf.sharedMesh);
					node.MaterialGuids = ToGuids(mr.sharedMaterials);
				}

				snap.Nodes.Add(node);
			}

			snap.Nodes.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
			snap.BlendShapes.Sort((a, b) =>
			{
				int p = string.CompareOrdinal(a.SmrPath, b.SmrPath);
				return p != 0 ? p : string.CompareOrdinal(a.BlendShapeName, b.BlendShapeName);
			});

			return snap;
		}

		internal static void Save(Snapshot snap, string filePath)
		{
			string? dir = System.IO.Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
			{
				System.IO.Directory.CreateDirectory(dir);
			}
			string json = JsonConvert.SerializeObject(snap, Formatting.Indented);
			System.IO.File.WriteAllText(filePath, json);
		}

		internal static Snapshot? Load(string filePath)
		{
			if (!System.IO.File.Exists(filePath)) return null;
			string json = System.IO.File.ReadAllText(filePath);
			return JsonConvert.DeserializeObject<Snapshot>(json);
		}

		private static string GetRelativePath(Transform root, Transform t)
		{
			if (t == root) return "";
			List<string> parts = new();
			Transform cur = t;
			while (cur != null && cur != root)
			{
				parts.Add(cur.name);
				cur = cur.parent;
			}
			if (cur != root)
			{
				ToolConsole.Warn("GoldenMaster", $"GetRelativePath: {t.name} は root {root.name} の子孫でない. __orphan として扱う");
				parts.Reverse();
				return "__orphan/" + string.Join("/", parts);
			}
			parts.Reverse();
			return string.Join("/", parts);
		}

		// Bake/Combine で生成された動的メッシュ・マテリアルは AssetPath が空になる.
		// その場合は asset 名で代替し, 名前そのものの一致をもって差分判定する.
		private static string? ToGuid(Object? asset)
		{
			if (asset == null) return null;
			string path = AssetDatabase.GetAssetPath(asset);
			if (!string.IsNullOrEmpty(path))
			{
				string guid = AssetDatabase.AssetPathToGUID(path);
				if (!string.IsNullOrEmpty(guid)) return guid;
			}
			return $"dynamic:{asset.GetType().Name}:{asset.name}";
		}

		private static string[] ToGuids(Object?[] assets)
		{
			if (assets == null) return System.Array.Empty<string>();
			string[] result = new string[assets.Length];
			for (int i = 0; i < assets.Length; i++) result[i] = ToGuid(assets[i]) ?? "";
			return result;
		}
	}
}

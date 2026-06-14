#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Entity;

namespace world.anlabo.mdnailtool.Editor.NailDesigns {
	internal static class NailPrefabBuilder {
		// Unity DefaultResources の GUID 固定値.
		private const string UNITY_DEFAULT_RES_GUID = "0000000000000000e000000000000000";

		// 内蔵 mesh fileID -> Resources.GetBuiltinResource 用ファイル名. CreatePrimitive 由来は "New-*.fbx".
		private static readonly Dictionary<long, string> BUILTIN_MESH_NAMES = new() {
			{ 10202, "Cube.fbx" }, { 10206, "New-Cylinder.fbx" }, { 10207, "New-Sphere.fbx" },
			{ 10208, "New-Capsule.fbx" }, { 10209, "New-Plane.fbx" }, { 10210, "Quad.fbx" },
		};

		internal static GameObject BuildFromNodes(NailPrefabNodeData[] rootNodes, string fallbackName) {
			if (rootNodes == null || rootNodes.Length == 0)
				return new GameObject(fallbackName);

			GameObject root = BuildSubtree(rootNodes[0], null);
			for (int i = 1; i < rootNodes.Length; i++)
				BuildSubtree(rootNodes[i], root.transform);
			return root;
		}

		private static Mesh? ResolveMesh(NailPrefabNodeData data) {
			if (string.IsNullOrEmpty(data.MeshGuid)) return null;
			if (data.MeshGuid == UNITY_DEFAULT_RES_GUID && data.MeshFileId.HasValue
			    && BUILTIN_MESH_NAMES.TryGetValue(data.MeshFileId.Value, out string? builtinName)) {
				return Resources.GetBuiltinResource<Mesh>(builtinName);
			}
			string meshPath = AssetDatabase.GUIDToAssetPath(data.MeshGuid!);
			return MDNailToolAssetLoader.LoadAssetSafe<Mesh>(meshPath);
		}

		private static Material?[] ResolveMaterials(string[]? guids) {
			if (guids == null || guids.Length == 0) return System.Array.Empty<Material?>();
			var result = new Material?[guids.Length];
			for (int i = 0; i < guids.Length; i++) {
				if (string.IsNullOrEmpty(guids[i])) { result[i] = null; continue; }
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				result[i] = MDNailToolAssetLoader.LoadAssetSafe<Material>(path);
			}
			return result;
		}

		private static GameObject BuildSubtree(NailPrefabNodeData data, Transform? parent) {
			var go = new GameObject(data.Name);
			if (parent != null) go.transform.SetParent(parent, false);

			if (data.LocalPosition != null && data.LocalPosition.Length >= 3)
				go.transform.localPosition = new Vector3(data.LocalPosition[0], data.LocalPosition[1], data.LocalPosition[2]);
			if (data.LocalRotation != null && data.LocalRotation.Length >= 4)
				go.transform.localRotation = new Quaternion(data.LocalRotation[0], data.LocalRotation[1], data.LocalRotation[2], data.LocalRotation[3]);
			if (data.LocalScale != null && data.LocalScale.Length >= 3)
				go.transform.localScale = new Vector3(data.LocalScale[0], data.LocalScale[1], data.LocalScale[2]);

			// RendererType 明示なし & MeshGuid あり = 既存 SMR データ (後方互換).
			string? rendererType = data.RendererType;
			if (rendererType == null && !string.IsNullOrEmpty(data.MeshGuid)) rendererType = "smr";

			if (rendererType == "smr") {
				Mesh? mesh = ResolveMesh(data);
				if (mesh != null) {
					var smr = go.AddComponent<SkinnedMeshRenderer>();
					smr.sharedMesh = mesh;
					smr.updateWhenOffscreen = true;

					// 全 node の 98.5% が center=[0,0.02,0] / extent=[0.02,0.02,0.02] のため null をデフォルトとして扱う.
					Vector3 boundsCenter = (data.BoundsCenter != null && data.BoundsCenter.Length >= 3)
						? new Vector3(data.BoundsCenter[0], data.BoundsCenter[1], data.BoundsCenter[2])
						: new Vector3(0f, 0.02f, 0f);
					Vector3 boundsSize = (data.BoundsExtent != null && data.BoundsExtent.Length >= 3)
						? new Vector3(data.BoundsExtent[0] * 2f, data.BoundsExtent[1] * 2f, data.BoundsExtent[2] * 2f)
						: new Vector3(0.04f, 0.04f, 0.04f);
					smr.localBounds = new Bounds(boundsCenter, boundsSize);

					if (data.BlendShapeWeights != null) {
						for (int i = 0; i < mesh.blendShapeCount; i++) {
							string bsName = mesh.GetBlendShapeName(i);
							if (data.BlendShapeWeights.TryGetValue(bsName, out float w))
								smr.SetBlendShapeWeight(i, w);
						}
					}
				}
			} else if (rendererType == "mr") {
				Mesh? mesh = ResolveMesh(data);
				if (mesh != null) {
					var mf = go.AddComponent<MeshFilter>();
					mf.sharedMesh = mesh;
				}
				var mr = go.AddComponent<MeshRenderer>();
				Material?[] mats = ResolveMaterials(data.MaterialGuids);
				if (mats.Length > 0) {
					var resolved = new Material[mats.Length];
					for (int i = 0; i < mats.Length; i++) resolved[i] = mats[i]!;
					mr.sharedMaterials = resolved;
				}
			}

			if (data.Children != null) {
				foreach (var child in data.Children)
					BuildSubtree(child, go.transform);
			}
			return go;
		}
	}
}

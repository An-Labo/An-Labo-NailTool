#nullable enable

using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Entity;

namespace world.anlabo.mdnailtool.Editor.NailDesigns {
	internal static class NailPrefabBuilder {
		internal static GameObject BuildFromNodes(NailPrefabNodeData[] rootNodes, string name) {
			var root = new GameObject(name);
			foreach (var nodeData in rootNodes)
				BuildNode(nodeData, root.transform);
			return root;
		}

		private static void BuildNode(NailPrefabNodeData data, Transform parent) {
			var go = new GameObject(data.Name);
			go.transform.SetParent(parent, false);

			if (data.LocalPosition != null && data.LocalPosition.Length >= 3)
				go.transform.localPosition = new Vector3(data.LocalPosition[0], data.LocalPosition[1], data.LocalPosition[2]);
			if (data.LocalRotation != null && data.LocalRotation.Length >= 4)
				go.transform.localRotation = new Quaternion(data.LocalRotation[0], data.LocalRotation[1], data.LocalRotation[2], data.LocalRotation[3]);
			if (data.LocalScale != null && data.LocalScale.Length >= 3)
				go.transform.localScale = new Vector3(data.LocalScale[0], data.LocalScale[1], data.LocalScale[2]);

			if (!string.IsNullOrEmpty(data.MeshGuid)) {
				string meshPath = AssetDatabase.GUIDToAssetPath(data.MeshGuid!);
				Mesh? mesh = MDNailToolAssetLoader.LoadAssetSafe<Mesh>(meshPath);
				if (mesh != null) {
					var smr = go.AddComponent<SkinnedMeshRenderer>();
					smr.sharedMesh = mesh;

					if (data.BlendShapeWeights != null) {
						for (int i = 0; i < mesh.blendShapeCount; i++) {
							string bsName = mesh.GetBlendShapeName(i);
							if (data.BlendShapeWeights.TryGetValue(bsName, out float w))
								smr.SetBlendShapeWeight(i, w);
						}
					}
				}
			}

			if (data.Children != null) {
				foreach (var child in data.Children)
					BuildNode(child, go.transform);
			}
		}
	}
}

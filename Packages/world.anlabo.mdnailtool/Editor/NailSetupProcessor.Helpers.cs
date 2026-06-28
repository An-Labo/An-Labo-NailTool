using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using Object = UnityEngine.Object;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public partial class NailSetupProcessor {
		private static Transform?[] GetHandsNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST
				.Select(name => FindNailObject(nailPrefabObject, name))
				.ToArray();
		}

		private static Transform?[] GetLeftFootNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
				.Select(name => FindNailObject(nailPrefabObject, name))
				.ToArray();
		}

		private static Transform?[] GetRightFootNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST
				.Select(name => FindNailObject(nailPrefabObject, name))
				.ToArray();
		}

		private static Transform? FindNailObject(GameObject nailPrefabObject, string name) {
			Transform? direct = nailPrefabObject.transform.Find(name);
			if (direct != null) return direct;
			return nailPrefabObject.GetComponentsInChildren<Transform>(true)
				.FirstOrDefault(t => t != nailPrefabObject.transform && t.name == name);
		}

		// メッシュ無ければベースからコピー
		private static void CopyMeshIfNull(Transform?[] variantNails, Transform?[] baseNails) {
			int count = Math.Min(variantNails.Length, baseNails.Length);
			for (int i = 0; i < count; i++) {
				if (variantNails[i] == null || baseNails[i] == null) continue;
				SkinnedMeshRenderer? varSmr = variantNails[i]!.GetComponent<SkinnedMeshRenderer>();
				if (varSmr == null) continue;
				if (varSmr.sharedMesh != null) continue;
				SkinnedMeshRenderer? baseSmr = baseNails[i]!.GetComponent<SkinnedMeshRenderer>();
				if (baseSmr == null || baseSmr.sharedMesh == null) continue;
				varSmr.sharedMesh = baseSmr.sharedMesh;
			}
		}

		internal static GameObject ResolveShapePrefab(GameObject basePrefab, string targetShape, NailPrefabNodeData[]? nailNodes = null) {
			string prefabPath = AssetDatabase.GetAssetPath(basePrefab);

			// in-memory orphan (BuildFromNodes 出力) は AssetPath 空. shape 別 disk prefab 探索不可なので NailNodes から target shape で再ビルド試行.
			// fallback 無しだと Process は Point で組み直すのに Preview は Natural のまま残り「scene 試着と着用結果で sizing がズレる」事故になる.
			if (string.IsNullOrEmpty(prefabPath)) {
				if (nailNodes != null && nailNodes.Length > 0) {
					NailPrefabNodeData[]? currentShapeNodes = null;
					using DBNailShape dbFb = new();
					foreach (NailShape ns in dbFb.collection) {
						string p = $"[{ns.ShapeName}]";
						NailPrefabNodeData[] found = Array.FindAll(nailNodes, n => n.Name != null && n.Name.StartsWith(p));
						if (found.Length > 0) currentShapeNodes = found;
						if (ns.ShapeName == targetShape) break;
					}
					if (currentShapeNodes != null) {
						return NailDesigns.NailPrefabBuilder.BuildFromNodes(currentShapeNodes, basePrefab.name);
					}
					return NailDesigns.NailPrefabBuilder.BuildFromNodes(nailNodes, basePrefab.name, targetShape);
				}
				return basePrefab;
			}

			System.Text.RegularExpressions.Regex nailPrefabNamePattern = new(@"(?<prefix>\[.+\])(?<prefabName>.+)");
			System.Text.RegularExpressions.Match match = nailPrefabNamePattern.Match(basePrefab.name);
			if (!match.Success) return basePrefab;

			string prefabName = match.Groups["prefabName"].Value;
			// Path.GetDirectoryName は Windows で `\` 区切りを返す. AssetDatabase は `/` 前提のため正規化する.
			string prefabDirPath = (Path.GetDirectoryName(prefabPath) ?? "").Replace('\\', '/');
			GameObject current = basePrefab;
			using DBNailShape dbNailShape = new();
			foreach (NailShape nailShape in dbNailShape.collection) {
				string newPrefabPath = $"{prefabDirPath}/[{nailShape.ShapeName}]{prefabName}.prefab";
				if (File.Exists(newPrefabPath)) {
					GameObject? newPrefab = NailSetupUtil.LoadPrefabAtPath(newPrefabPath);
					if (newPrefab != null) current = newPrefab;
				}
				if (nailShape.ShapeName == targetShape) break;
			}
			return current;
		}

		private static string GetRelativePath(Transform root, Transform target) {
			var parts = new List<string>();
			Transform? current = target;
			while (current != null && current != root) {
				parts.Insert(0, current.name);
				current = current.parent;
			}
			return string.Join("/", parts);
		}

		// FBX素体基準でネイルの位置と回転を計算
		internal static Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>
			ComputeScaleCompensatedTransforms(
				VRCAvatarDescriptor avatar,
				Dictionary<string, Transform?> targetBoneDictionary,
				Transform?[] nailObjects,
				int[] boneIndices)
		{
			var result = new Dictionary<Transform, (Vector3, Quaternion, Vector3)>();

			Animator? avatarAnimator = avatar.GetComponent<Animator>();
			if (avatarAnimator == null || avatarAnimator.avatar == null) return result;

			string modelPath = AssetDatabase.GetAssetPath(avatarAnimator.avatar);
			if (string.IsNullOrEmpty(modelPath)) return result;

			GameObject? referenceAsset = MDNailToolAssetLoader.LoadPrefabSafe(modelPath);
			if (referenceAsset == null) return result;

			GameObject tempInstance = Object.Instantiate(referenceAsset);
			try
			{
				tempInstance.transform.SetPositionAndRotation(avatar.transform.position, avatar.transform.rotation);
				tempInstance.transform.localScale = avatar.transform.lossyScale;

				Dictionary<string, Transform> tempBonesByName = new();
				foreach (Transform t in tempInstance.GetComponentsInChildren<Transform>())
				{
					if (!tempBonesByName.ContainsKey(t.name))
						tempBonesByName[t.name] = t;
				}

				for (int i = 0; i < nailObjects.Length; i++)
				{
					Transform? nail = nailObjects[i];
					if (nail == null) continue;
					if (boneIndices[i] < 0 || boneIndices[i] >= MDNailToolDefines.TARGET_BONE_NAME_LIST.Count) continue;

					string boneName = MDNailToolDefines.TARGET_BONE_NAME_LIST[boneIndices[i]];
					Transform? actualBone = targetBoneDictionary.GetValueOrDefault(boneName);
					if (actualBone == null) continue;

					if (!tempBonesByName.TryGetValue(actualBone.name, out Transform? tempBone)) continue;

					Vector3 localPos = tempBone.InverseTransformPoint(nail.position);
					Quaternion localRot = Quaternion.Inverse(tempBone.rotation) * nail.rotation;

					Vector3 correctedWorldPos = actualBone.TransformPoint(localPos);
					Quaternion correctedWorldRot = actualBone.rotation * localRot;

					result[nail] = (correctedWorldPos, correctedWorldRot, nail.lossyScale);
				}
			}
			finally
			{
				Object.DestroyImmediate(tempInstance);
			}

			return result;
		}

		// サイズを目標値に揃える
		internal static void EnforceLossyScale(Transform nail, Vector3 desiredLossyScale)
		{
			if (nail == null) return;
			const int MAX_ITER = 6;
			const float TOLERANCE = 0.001f;
			for (int i = 0; i < MAX_ITER; i++)
			{
				Vector3 cur = nail.lossyScale;
				float rx = Mathf.Abs(cur.x) > 1e-6f ? desiredLossyScale.x / cur.x : 1f;
				float ry = Mathf.Abs(cur.y) > 1e-6f ? desiredLossyScale.y / cur.y : 1f;
				float rz = Mathf.Abs(cur.z) > 1e-6f ? desiredLossyScale.z / cur.z : 1f;
				if (Mathf.Abs(rx - 1f) < TOLERANCE && Mathf.Abs(ry - 1f) < TOLERANCE && Mathf.Abs(rz - 1f) < TOLERANCE)
					break;
				Vector3 ls = nail.localScale;
				nail.localScale = new Vector3(ls.x * rx, ls.y * ry, ls.z * rz);
			}
		}
	}
}

#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;

namespace world.anlabo.mdnailtool.Editor
{
	public static partial class NailSetupUtil
	{
		public static void ReplaceHandsNailMesh(Transform?[] handsNailObjects, Mesh?[] overrideMesh)
		{
			if (overrideMesh.Length != 10)
			{
				throw new NailToolDeveloperException("NailSetup", $"Incorrect length of {nameof(overrideMesh)} parameter : {overrideMesh.Length}");
			}

			if (handsNailObjects.Length != 10)
			{
				throw new NailToolDeveloperException("NailSetup", $"Incorrect length of {nameof(handsNailObjects)} parameter : {handsNailObjects.Length}");
			}

			ReplaceMesh(handsNailObjects, overrideMesh);
		}

		public static void ReplaceFootNailMesh(Transform?[] leftFootNailObjects, Transform?[] rightFootNailObjects, string nailShape)
		{
			if (leftFootNailObjects.Length != 5)
			{
				throw new NailToolDeveloperException("NailSetup", $"Incorrect length of {nameof(leftFootNailObjects)} parameter : {leftFootNailObjects.Length}");
			}

			if (rightFootNailObjects.Length != 5)
			{
				throw new NailToolDeveloperException("NailSetup", $"Incorrect length of {nameof(rightFootNailObjects)} parameter : {rightFootNailObjects.Length}");
			}

			using DBNailShape db = new();
			NailShape? shape = db.FindNailShapeByName(nailShape);
			if (shape == null)
			{
				throw new NailToolDeveloperException("NailSetup", "Not found nail shape.");
			}

			string? path = null;
			foreach (string guid in shape.FootFbxFolderGUID)
			{
				if (string.IsNullOrEmpty(guid)) continue;
				path = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrEmpty(path)) continue;
				if (Directory.Exists(path)) break;
			}

			if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
			{
				throw new NailToolDeveloperException("NailSetup", "Not found foot nail objects.");
			}


			Mesh[] leftFootOverrideMesh;
			Mesh[] rightFootOverrideMesh;
			if (File.Exists($"{path}/{shape.FootFbxNamePrefix}{MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST[0]}.fbx"))
			{
				leftFootOverrideMesh = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName}.fbx")
					.Select(p => MDNailToolAssetLoader.LoadAssetSafe<Mesh>(p)!)
					.ToArray();

				rightFootOverrideMesh = MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName}.fbx")
					.Select(p => MDNailToolAssetLoader.LoadAssetSafe<Mesh>(p)!)
					.ToArray();
			}
			else
			{
				leftFootOverrideMesh = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName.Replace('.', '_')}.fbx")
					.Select(p => MDNailToolAssetLoader.LoadAssetSafe<Mesh>(p)!)
					.ToArray();

				rightFootOverrideMesh = MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName.Replace('.', '_')}.fbx")
					.Select(p => MDNailToolAssetLoader.LoadAssetSafe<Mesh>(p)!)
					.ToArray();

			}

			ReplaceMesh(leftFootNailObjects, leftFootOverrideMesh);
			ReplaceMesh(rightFootNailObjects, rightFootOverrideMesh);
		}

		private static void ReplaceMesh(Transform?[] transforms, Mesh?[] overrideMesh)
		{
			for (int index = 0; index < overrideMesh.Length; index++)
			{
				Mesh? newMesh = overrideMesh[index];
				if (newMesh == null) continue;
				Transform? targetTransform = transforms[index];
				if (targetTransform == null) continue;

				SkinnedMeshRenderer? smr = targetTransform.GetComponent<SkinnedMeshRenderer>();
				if (smr == null) continue;

				Dictionary<string, float> savedWeights = new();
				Mesh currentMesh = smr.sharedMesh;
				if (currentMesh != null)
				{
					for (int i = 0; i < currentMesh.blendShapeCount; i++)
					{
						savedWeights[currentMesh.GetBlendShapeName(i)] = smr.GetBlendShapeWeight(i);
					}
				}

				// enabled トグルで内部頂点バッファを再確保させ、差し替え時の描画停止を防ぐ (issue #495)
				smr.enabled = false;
				smr.sharedMesh = newMesh;
				smr.enabled = true;

				foreach (var weightInfo in savedWeights)
				{
					int newIndex = newMesh.GetBlendShapeIndex(weightInfo.Key);
					if (newIndex != -1)
					{
						smr.SetBlendShapeWeight(newIndex, weightInfo.Value);
					}
				}
			}
		}
	}
}

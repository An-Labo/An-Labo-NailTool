#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace world.anlabo.mdnailtool.Editor
{
	public static partial class NailSetupUtil
	{
		public static (Vector3[] deltaV, Vector3[] deltaN, Vector3[] deltaT) ComputeBlendShapeDelta(Mesh baseMesh, Mesh variantMesh)
		{
			int count = baseMesh.vertexCount;
			Vector3[] baseV = baseMesh.vertices;
			Vector3[] baseN = baseMesh.normals;
			Vector4[] baseT = baseMesh.tangents;
			Vector3[] varV = variantMesh.vertices;
			Vector3[] varN = variantMesh.normals;
			Vector4[] varT = variantMesh.tangents;

			Vector3[] deltaV = new Vector3[count];
			Vector3[] deltaN = new Vector3[count];
			Vector3[] deltaT = new Vector3[count];

			for (int i = 0; i < count; i++)
			{
				deltaV[i] = varV[i] - baseV[i];
				deltaN[i] = (varN.Length > i ? varN[i] : Vector3.zero) - (baseN.Length > i ? baseN[i] : Vector3.zero);
				Vector4 bt = baseT.Length > i ? baseT[i] : Vector4.zero;
				Vector4 vt = varT.Length > i ? varT[i] : Vector4.zero;
				deltaT[i] = new Vector3(vt.x - bt.x, vt.y - bt.y, vt.z - bt.z);
			}
			return (deltaV, deltaN, deltaT);
		}

		public static void BakeBlendShapesToNails(
			IEnumerable<Transform?> nailObjects,
			IEnumerable<SkinnedMeshRenderer> sourceSmrs,
			string saveBasePath,
			IReadOnlyDictionary<string, float>? initialWeights = null)
		{
			if (!Directory.Exists(saveBasePath))
				Directory.CreateDirectory(saveBasePath);

			foreach (Transform? nailObj in nailObjects)
			{
				if (nailObj == null) continue;
				SkinnedMeshRenderer? nailSmr = nailObj.GetComponent<SkinnedMeshRenderer>();
				if (nailSmr == null || nailSmr.sharedMesh == null) continue;

				Mesh originalMesh = nailSmr.sharedMesh;
				Mesh newMesh = UnityEngine.Object.Instantiate(originalMesh);
				newMesh.name = $"{originalMesh.name}_bs";

				foreach (SkinnedMeshRenderer sourceSmr in sourceSmrs)
				{
					Mesh? sourceMesh = sourceSmr.sharedMesh;
					if (sourceMesh == null) continue;

					for (int si = 0; si < sourceMesh.blendShapeCount; si++)
					{
						string shapeName = sourceMesh.GetBlendShapeName(si);
						if (newMesh.GetBlendShapeIndex(shapeName) >= 0) continue;

						int frameCount = sourceMesh.GetBlendShapeFrameCount(si);
						for (int fi = 0; fi < frameCount; fi++)
						{
							float weight = sourceMesh.GetBlendShapeFrameWeight(si, fi);
							var dv = new Vector3[newMesh.vertexCount];
							var dn = new Vector3[newMesh.vertexCount];
							var dt = new Vector3[newMesh.vertexCount];
							newMesh.AddBlendShapeFrame(shapeName, weight, dv, dn, dt);
						}
					}
				}

				string assetPath = $"{saveBasePath}/{newMesh.name}.asset";
				AssetDatabase.CreateAsset(newMesh, assetPath);
				nailSmr.sharedMesh = newMesh;

				if (initialWeights != null)
				{
					foreach (var (shapeNameKey, weight) in initialWeights)
					{
						string normalizedKey = shapeNameKey.Replace(" ", "").Replace("　", "");

						for (int i = 0; i < newMesh.blendShapeCount; i++)
						{
							string bn = newMesh.GetBlendShapeName(i);
							if (bn.Replace(" ", "").Replace("　", "") == normalizedKey)
							{
								nailSmr.SetBlendShapeWeight(i, weight);
								break;
							}
						}
					}
				}

				foreach (SkinnedMeshRenderer sourceSmr in sourceSmrs)
				{
					Mesh? sourceMesh = sourceSmr.sharedMesh;
					if (sourceMesh == null) continue;

					for (int si = 0; si < sourceMesh.blendShapeCount; si++)
					{
						string shapeName = sourceMesh.GetBlendShapeName(si);
						int nailIdx = newMesh.GetBlendShapeIndex(shapeName);
						if (nailIdx >= 0)
						{
							float srcWeight = sourceSmr.GetBlendShapeWeight(si);
							if (srcWeight != 0f)
								nailSmr.SetBlendShapeWeight(nailIdx, srcWeight);
						}
					}
				}
			}
			AssetDatabase.SaveAssets();
		}

	}
}

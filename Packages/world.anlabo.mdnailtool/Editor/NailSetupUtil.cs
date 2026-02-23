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
	public static class NailSetupUtil
	{

		public static void ReplaceHandsNailMesh(Transform?[] handsNailObjects, Mesh?[] overrideMesh)
		{
			if (overrideMesh.Length != 10)
			{
				throw new ArgumentException($"Incorrect length of {nameof(overrideMesh)} parameter : {overrideMesh.Length}");
			}

			if (handsNailObjects.Length != 10)
			{
				throw new ArgumentException($"Incorrect length of {nameof(handsNailObjects)} parameter : {overrideMesh.Length}");
			}

			ReplaceMesh(handsNailObjects, overrideMesh);
		}

		public static void ReplaceFootNailMesh(Transform?[] leftFootNailObjects, Transform?[] rightFootNailObjects, string nailShape)
		{
			if (leftFootNailObjects.Length != 5)
			{
				throw new ArgumentException($"Incorrect length of {nameof(leftFootNailObjects)} parameter : {leftFootNailObjects.Length}");
			}

			if (rightFootNailObjects.Length != 5)
			{
				throw new ArgumentException($"Incorrect length of {nameof(rightFootNailObjects)} parameter : {rightFootNailObjects.Length}");
			}

			using DBNailShape db = new();
			NailShape? shape = db.FindNailShapeByName(nailShape);
			if (shape == null)
			{
				throw new ArgumentException("Not found nail shape.");
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
				throw new InvalidOperationException("Not found foot nail objects.");
			}


			Mesh[] leftFootOverrideMesh;
			Mesh[] rightFootOverrideMesh;
			if (File.Exists($"{path}/{shape.FootFbxNamePrefix}{MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST[0]}.fbx"))
			{
				leftFootOverrideMesh = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName}.fbx")
					.Select(AssetDatabase.LoadAssetAtPath<Mesh>)
					.ToArray();

				rightFootOverrideMesh = MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName}.fbx")
					.Select(AssetDatabase.LoadAssetAtPath<Mesh>)
					.ToArray();
			}
			else
			{
				leftFootOverrideMesh = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName.Replace('.', '_')}.fbx")
					.Select(AssetDatabase.LoadAssetAtPath<Mesh>)
					.ToArray();

				rightFootOverrideMesh = MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName.Replace('.', '_')}.fbx")
					.Select(name =>
					{
						Debug.Log(name);
						return name;
					})
					.Select(AssetDatabase.LoadAssetAtPath<Mesh>)
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

				smr.sharedMesh = newMesh;

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

		public static void ReplaceNailMaterial(Transform?[] handsNailObjects, IEnumerable<Transform?> leftFootNailObjects, IEnumerable<Transform?> rightFootNailObjects,
			(INailProcessor, string, string)[] nailDesignAndVariationNames, string nailShapeName, bool isGenerate, bool isPreview, Material? overrideMaterial = null)
		{

			if (overrideMaterial != null)
			{
				ApplyOverrideMaterialToAll(handsNailObjects, overrideMaterial);
				ApplyOverrideMaterialToAll(leftFootNailObjects, overrideMaterial);
				ApplyOverrideMaterialToAll(rightFootNailObjects, overrideMaterial);
				return;
			}

			if (nailDesignAndVariationNames.Length != 20)
			{
				throw new ArgumentException($"Incorrect length of {nameof(nailDesignAndVariationNames)} parameter : {nailDesignAndVariationNames.Length}");
			}

			for (int index = 0; index < handsNailObjects.Length; index++)
			{
				(INailProcessor processor, string materialName, string colorName) = nailDesignAndVariationNames[index];

				Transform? transform = handsNailObjects[index];
				if (transform == null)
				{
					continue;
				}

				ApplyMaterial(transform, processor, materialName, colorName, nailShapeName, isGenerate, isPreview);
			}

			var leftFootArray = leftFootNailObjects.ToArray();
			for (int i = 0; i < leftFootArray.Length; i++)
			{
				int designIndex = 10 + i;
				if (designIndex >= nailDesignAndVariationNames.Length) break;

				(INailProcessor processor, string materialName, string colorName) = nailDesignAndVariationNames[designIndex];
				Transform? transform = leftFootArray[i];

				if (transform == null) continue;
				ApplyMaterial(transform, processor, materialName, colorName, nailShapeName, isGenerate, isPreview);
			}

			var rightFootArray = rightFootNailObjects.ToArray();
			for (int i = 0; i < rightFootArray.Length; i++)
			{
				int designIndex = 15 + i;
				if (designIndex >= nailDesignAndVariationNames.Length) break;

				(INailProcessor processor, string materialName, string colorName) = nailDesignAndVariationNames[designIndex];
				Transform? transform = rightFootArray[i];

				if (transform == null) continue;
				ApplyMaterial(transform, processor, materialName, colorName, nailShapeName, isGenerate, isPreview);
			}
		}

		private static void ApplyMaterial(Transform transform, INailProcessor processor, string materialName, string colorName, string nailShapeName, bool isGenerate, bool isPreview)
		{
			Renderer? renderer = transform.GetComponent<Renderer>();
			if (renderer == null)
			{
				Debug.LogError($"Not found Renderer : {transform.name}");
				return;
			}

			if (processor == null) return;

			Material mainMaterial = processor.GetMaterial(materialName, colorName, nailShapeName, isGenerate, isPreview);
			IEnumerable<Material> additionalMaterial = processor.GetAdditionalMaterials(colorName, nailShapeName, isPreview);
			renderer.sharedMaterials = additionalMaterial.Prepend(mainMaterial).ToArray();
		}

		public static void AttachAdditionalObjects(Transform?[] handsNailObjects, (INailProcessor, string, string)[] nailDesignAndVariationNames, string nailShapeName, bool isPreview)
		{
			if (handsNailObjects.Length != 10)
			{
				throw new ArgumentException($"Incorrect length of {nameof(handsNailObjects)} parameter : {handsNailObjects.Length}");
			}

			for (int index = 0; index < handsNailObjects.Length; index++)
			{
				(INailProcessor processor, string _, string colorName) = nailDesignAndVariationNames[index];

				Transform? transform = handsNailObjects[index];
				if (transform == null)
				{
					continue;
				}

				if (processor == null) continue;

				foreach (Transform additionalObject in processor.GetAdditionalObjects(colorName, nailShapeName, (MDNailToolDefines.TargetFinger)index, isPreview))
				{
					additionalObject.SetParent(transform, false);
				}
			}
		}
		private static void ApplyOverrideMaterialToAll(IEnumerable<Transform?> nailObjects, Material overrideMaterial)
		{
			foreach (Transform? nailObject in nailObjects)
			{
				if (nailObject == null) continue;
				Renderer? renderer = nailObject.GetComponent<Renderer>();
				if (renderer == null) continue;

				Material[] materials = renderer.sharedMaterials;
				for (int i = 0; i < materials.Length; i++)
				{
					materials[i] = overrideMaterial;
				}
				renderer.sharedMaterials = materials;
			}
		}

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

		public static GameObject? BakeAndCombineNailMeshes(
			Transform?[] nailObjects,
			GameObject nailPrefabObject,
			string zoneName,
			string saveBasePath,
			(string Name, Transform?[] VariantNails)[]? variants = null)
		{
			var validPairs = nailObjects
				.Where(t => t != null && t.GetComponent<SkinnedMeshRenderer>() != null
				            && t.GetComponent<SkinnedMeshRenderer>()!.sharedMesh != null)
				.Select(t => (transform: t!, smr: t!.GetComponent<SkinnedMeshRenderer>()!))
				.ToArray();
			if (validPairs.Length == 0) return null;

			GameObject combinedGo = new GameObject(zoneName);
			combinedGo.transform.SetParent(nailPrefabObject.transform, false);
			combinedGo.transform.localPosition = Vector3.zero;
			combinedGo.transform.localRotation = Quaternion.identity;
			combinedGo.transform.localScale    = Vector3.one;

			var boneTransforms = new Transform[validPairs.Length];
			for (int i = 0; i < validPairs.Length; i++)
			{
				boneTransforms[i] = validPairs[i].transform.parent;
			}

			var combinedMesh = new Mesh();
			combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			combinedMesh.name = zoneName;

			var allVerts    = new List<Vector3>();
			var allNormals  = new List<Vector3>();
			var allUVs      = new List<Vector2>();
			var allWeights  = new List<BoneWeight>();
			int[] vertexOffsets = new int[validPairs.Length];
			int vertexOffset = 0;

			Matrix4x4 combinedGoW2L = combinedGo.transform.worldToLocalMatrix;

			for (int si = 0; si < validPairs.Length; si++)
			{
				vertexOffsets[si] = vertexOffset;
				Mesh mesh = validPairs[si].smr.sharedMesh;

				Mesh bakedMesh = new Mesh();
				validPairs[si].smr.BakeMesh(bakedMesh);

				Matrix4x4 toLocal = combinedGoW2L * validPairs[si].transform.localToWorldMatrix;

				Vector3[] srcVerts   = bakedMesh.vertices;
				Vector3[] srcNormals = bakedMesh.normals;
				for (int vi = 0; vi < mesh.vertexCount; vi++)
				{
					allVerts.Add(toLocal.MultiplyPoint3x4(srcVerts[vi]));
					Vector3 n = srcNormals.Length > vi ? srcNormals[vi] : Vector3.up;
					allNormals.Add(toLocal.MultiplyVector(n).normalized);
				}

				Vector2[] uvs = mesh.uv;
				allUVs.AddRange(uvs.Length == mesh.vertexCount ? uvs : new Vector2[mesh.vertexCount]);

				for (int vi = 0; vi < mesh.vertexCount; vi++)
					allWeights.Add(new BoneWeight { boneIndex0 = si, weight0 = 1f });

				vertexOffset += mesh.vertexCount;

				UnityEngine.Object.DestroyImmediate(bakedMesh);
			}

			combinedMesh.vertices    = allVerts.ToArray();
			combinedMesh.normals     = allNormals.ToArray();
			combinedMesh.uv          = allUVs.ToArray();
			combinedMesh.boneWeights = allWeights.ToArray();

			var materialGroups = new Dictionary<Material, List<int>>();
			var materialList = new List<Material>();

			for (int si = 0; si < validPairs.Length; si++)
			{
				Material mat = validPairs[si].smr.sharedMaterials.Length > 0 ? validPairs[si].smr.sharedMaterials[0] : null!;
				if (mat == null) continue;

				if (!materialGroups.ContainsKey(mat))
				{
					materialGroups[mat] = new List<int>();
					materialList.Add(mat);
				}

				int[] srcTris = validPairs[si].smr.sharedMesh.triangles;
				int vOff = vertexOffsets[si];
				for (int ti = 0; ti < srcTris.Length; ti++)
				{
					materialGroups[mat].Add(srcTris[ti] + vOff);
				}
			}

			combinedMesh.subMeshCount = materialList.Count;
			for (int mi = 0; mi < materialList.Count; mi++)
			{
				Material mat = materialList[mi];
				combinedMesh.SetTriangles(materialGroups[mat].ToArray(), mi);
			}

			Matrix4x4 combinedL2W = combinedGo.transform.localToWorldMatrix;
			combinedMesh.bindposes = boneTransforms
				.Select(b => b.worldToLocalMatrix * combinedL2W)
				.ToArray();

			var allOriginalShapeNames = new List<string>();
			foreach (var (_, smr) in validPairs)
				for (int shi = 0; shi < smr.sharedMesh.blendShapeCount; shi++)
				{
					string sn = smr.sharedMesh.GetBlendShapeName(shi);
					if (!allOriginalShapeNames.Contains(sn)) allOriginalShapeNames.Add(sn);
				}

			int totalVertCount = allVerts.Count;

			if (variants != null)
			{
				foreach (var variant in variants)
				{
					string shapeName = variant.Name;
					string normalizedVariantName = variant.Name.Replace(" ", "").Replace("　", "");
					foreach (var originalName in allOriginalShapeNames) {
						if (originalName.Replace(" ", "").Replace("　", "") == normalizedVariantName) {
							shapeName = originalName;
							break;
						}
					}
					var fullDv = new Vector3[totalVertCount];
					var fullDn = new Vector3[totalVertCount];
					var fullDt = new Vector3[totalVertCount];

					int vOff = 0;
					bool hasAnyDelta = false;

					for (int si = 0; si < validPairs.Length; si++)
					{
						Transform baseNail = validPairs[si].transform;
						Mesh baseMesh = validPairs[si].smr.sharedMesh;
						int siVertCount = baseMesh.vertexCount;

						Transform? variantNail = variant.VariantNails.FirstOrDefault(t => t != null && t.name == baseNail.name);

						if (variantNail != null)
						{
						SkinnedMeshRenderer? varSmr = variantNail.GetComponent<SkinnedMeshRenderer>();
						if (varSmr != null && varSmr.sharedMesh != null && varSmr.sharedMesh.vertexCount == siVertCount)
						{
							hasAnyDelta = true;

							Mesh bakedBaseMesh = new Mesh();
							validPairs[si].smr.BakeMesh(bakedBaseMesh);

							Mesh bakedVarMesh = new Mesh();
							varSmr.BakeMesh(bakedVarMesh);

							Matrix4x4 variantToLocal = combinedGoW2L * variantNail.localToWorldMatrix;
							Matrix4x4 baseToLocal = combinedGoW2L * baseNail.localToWorldMatrix;

							Vector3[] varVerts = bakedVarMesh.vertices;
							Vector3[] baseVerts = bakedBaseMesh.vertices;
							Vector3[] varNormals = bakedVarMesh.normals;
							Vector3[] baseNormals = bakedBaseMesh.normals;

							for (int vi = 0; vi < siVertCount; vi++)
							{
								Vector3 vv = variantToLocal.MultiplyPoint3x4(varVerts[vi]);
								Vector3 bv = baseToLocal.MultiplyPoint3x4(baseVerts[vi]);
								fullDv[vOff + vi] = vv - bv;

								Vector3 vn = varNormals.Length > vi ? varNormals[vi] : Vector3.up;
								Vector3 bn = baseNormals.Length > vi ? baseNormals[vi] : Vector3.up;
								Vector3 w_vn = variantToLocal.MultiplyVector(vn).normalized;
								Vector3 w_bn = baseToLocal.MultiplyVector(bn).normalized;
								fullDn[vOff + vi] = w_vn - w_bn;

								fullDt[vOff + vi] = Vector3.zero;
							}

							UnityEngine.Object.DestroyImmediate(bakedBaseMesh);
							UnityEngine.Object.DestroyImmediate(bakedVarMesh);
							}
						}

						vOff += siVertCount;
					}

					if (hasAnyDelta)
					{
						combinedMesh.AddBlendShapeFrame(shapeName, 100f, fullDv, fullDn, fullDt);
					}
				}
			}

			if (!Directory.Exists(saveBasePath))
				Directory.CreateDirectory(saveBasePath);
			string assetPath = $"{saveBasePath}/{zoneName}.asset";
			AssetDatabase.CreateAsset(combinedMesh, assetPath);

			SkinnedMeshRenderer combinedSmr = combinedGo.AddComponent<SkinnedMeshRenderer>();
			combinedSmr.sharedMesh = combinedMesh;
			combinedSmr.bones      = boneTransforms;
			combinedSmr.rootBone   = boneTransforms[0];

			combinedSmr.sharedMaterials = materialList.ToArray();

			for (int bsIdx = 0; bsIdx < combinedMesh.blendShapeCount; bsIdx++)
			{
				combinedSmr.SetBlendShapeWeight(bsIdx, 0f);
			}

			foreach (var (t, _) in validPairs)
				UnityEngine.Object.DestroyImmediate(t.gameObject);

			return combinedGo;
		}
	}
}
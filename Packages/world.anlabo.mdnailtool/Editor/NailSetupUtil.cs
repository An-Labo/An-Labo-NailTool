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
			(INailProcessor, string, string)[] nailDesignAndVariationNames, string nailShapeName, bool isGenerate, bool isPreview, Material? overrideMaterial = null,
			bool enableAdditionalMaterials = true, IEnumerable<Material>?[]? perFingerAdditionalMaterials = null)
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

				IEnumerable<Material>? fingerAdditional = perFingerAdditionalMaterials != null && index < perFingerAdditionalMaterials.Length
					? perFingerAdditionalMaterials[index] : null;
				ApplyMaterial(transform, processor, materialName, colorName, nailShapeName, isGenerate, isPreview, enableAdditionalMaterials, fingerAdditional);
			}

			var leftFootArray = leftFootNailObjects.ToArray();
			for (int i = 0; i < leftFootArray.Length; i++)
			{
				int designIndex = 10 + i;
				if (designIndex >= nailDesignAndVariationNames.Length) break;

				(INailProcessor processor, string materialName, string colorName) = nailDesignAndVariationNames[designIndex];
				Transform? transform = leftFootArray[i];

				if (transform == null) continue;
				IEnumerable<Material>? fingerAdditional = perFingerAdditionalMaterials != null && designIndex < perFingerAdditionalMaterials.Length
					? perFingerAdditionalMaterials[designIndex] : null;
				ApplyMaterial(transform, processor, materialName, colorName, nailShapeName, isGenerate, isPreview, enableAdditionalMaterials, fingerAdditional);
			}

			var rightFootArray = rightFootNailObjects.ToArray();
			for (int i = 0; i < rightFootArray.Length; i++)
			{
				int designIndex = 15 + i;
				if (designIndex >= nailDesignAndVariationNames.Length) break;

				(INailProcessor processor, string materialName, string colorName) = nailDesignAndVariationNames[designIndex];
				Transform? transform = rightFootArray[i];

				if (transform == null) continue;
				IEnumerable<Material>? fingerAdditional = perFingerAdditionalMaterials != null && designIndex < perFingerAdditionalMaterials.Length
					? perFingerAdditionalMaterials[designIndex] : null;
				ApplyMaterial(transform, processor, materialName, colorName, nailShapeName, isGenerate, isPreview, enableAdditionalMaterials, fingerAdditional);
			}
		}

		private static void ApplyMaterial(Transform transform, INailProcessor processor, string materialName, string colorName, string nailShapeName, bool isGenerate, bool isPreview,
			bool enableAdditionalMaterials = true, IEnumerable<Material>? overrideAdditionalMaterials = null)
		{
			Renderer? renderer = transform.GetComponent<Renderer>();
			if (renderer == null)
			{
				Debug.LogError($"Not found Renderer : {transform.name}");
				return;
			}

			if (processor == null) return;

			Material mainMaterial = processor.GetMaterial(materialName, colorName, nailShapeName, isGenerate, isPreview);

			if (enableAdditionalMaterials)
			{
				IEnumerable<Material> additionalMaterial = overrideAdditionalMaterials
					?? processor.GetAdditionalMaterials(colorName, nailShapeName, isPreview);
				renderer.sharedMaterials = additionalMaterial.Prepend(mainMaterial).ToArray();
			}
			else
			{
				renderer.sharedMaterials = new[] { mainMaterial };
			}
		}

		public static void AttachAdditionalObjects(Transform?[] handsNailObjects, (INailProcessor, string, string)[] nailDesignAndVariationNames, string nailShapeName, bool isPreview,
			IEnumerable<Transform>?[]? perFingerAdditionalObjects = null)
		{
			ToolConsole.Log($"AttachAdditionalObjects: perFinger null? {perFingerAdditionalObjects == null}, isPreview={isPreview}");
			if (handsNailObjects.Length != 10)
			{
				throw new ArgumentException($"Incorrect length of {nameof(handsNailObjects)} parameter : {handsNailObjects.Length}");
			}

			for (int index = 0; index < handsNailObjects.Length; index++)
			{
				Transform? transform = handsNailObjects[index];
				if (transform == null)
				{
					ToolConsole.Log($"  index={index}: transform=null → skip");
					continue;
				}

				// per-finger オーバーライドがあればそちらを使用
				IEnumerable<Transform>? fingerObjects = perFingerAdditionalObjects != null && index < perFingerAdditionalObjects.Length
					? perFingerAdditionalObjects[index] : null;

				ToolConsole.Log($"  index={index}: transform={transform.name}, fingerObjects null? {fingerObjects == null}");

				if (fingerObjects != null)
				{
					int count = 0;
					foreach (Transform additionalObject in fingerObjects)
					{
						additionalObject.SetParent(transform, false);
						count++;
					}
					ToolConsole.Log($"  index={index}: attached {count} per-finger objects");
				}
				else
				{
					// フォールバック: processor から取得
					(INailProcessor processor, string _, string colorName) = nailDesignAndVariationNames[index];
					if (processor == null)
					{
						ToolConsole.Log($"  index={index}: processor=null → skip");
						continue;
					}

					int count = 0;
					foreach (Transform additionalObject in processor.GetAdditionalObjects(colorName, nailShapeName, (MDNailToolDefines.TargetFinger)index, isPreview))
					{
						additionalObject.SetParent(transform, false);
						count++;
					}
					ToolConsole.Log($"  index={index}: attached {count} fallback objects via processor");
				}
			}
		}

		/// <summary>
		/// レンダラーが使用する全テクスチャのMip Streamingを有効化する。
		/// mipmapEnabled=falseのテクスチャはスキップ（ミップマップ自体が無いため）。
		/// </summary>
		public static void EnableMipStreamingForRenderers(IEnumerable<Renderer?> renderers)
		{
			var pathsToReimport = new HashSet<string>();

			foreach (Renderer? renderer in renderers)
			{
				if (renderer == null) continue;
				foreach (Material mat in renderer.sharedMaterials)
				{
					if (mat == null) continue;
					foreach (int propId in mat.GetTexturePropertyNameIDs())
					{
						Texture? tex = mat.GetTexture(propId);
						if (tex == null) continue;
						string texPath = AssetDatabase.GetAssetPath(tex);
						if (string.IsNullOrEmpty(texPath) || pathsToReimport.Contains(texPath)) continue;

						TextureImporter? importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
						if (importer == null) continue;
						if (importer.streamingMipmaps) continue;
						if (!importer.mipmapEnabled) continue;

						importer.streamingMipmaps = true;
						pathsToReimport.Add(texPath);
					}
				}
			}

			if (pathsToReimport.Count == 0) return;

			AssetDatabase.StartAssetEditing();
			try
			{
				foreach (string path in pathsToReimport)
					AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
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


		// 三角形上の最近傍点を計算
		private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
		{
			Vector3 ab = b - a, ac = c - a, ap = p - a;
			float d1 = Vector3.Dot(ab, ap);
			float d2 = Vector3.Dot(ac, ap);
			if (d1 <= 0f && d2 <= 0f) return a;

			Vector3 bp = p - b;
			float d3 = Vector3.Dot(ab, bp);
			float d4 = Vector3.Dot(ac, bp);
			if (d3 >= 0f && d4 <= d3) return b;

			float vc = d1 * d4 - d3 * d2;
			if (vc <= 0f && d1 >= 0f && d3 <= 0f)
				return a + (d1 / (d1 - d3)) * ab;

			Vector3 cp = p - c;
			float d5 = Vector3.Dot(ab, cp);
			float d6 = Vector3.Dot(ac, cp);
			if (d6 >= 0f && d5 <= d6) return c;

			float vb = d5 * d2 - d1 * d6;
			if (vb <= 0f && d2 >= 0f && d6 <= 0f)
				return a + (d2 / (d2 - d6)) * ac;

			float va = d3 * d6 - d5 * d4;
			if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
				return b + ((d4 - d3) / ((d4 - d3) + (d5 - d6))) * (c - b);

			float denom = 1f / (va + vb + vc);
			return a + ab * (vb * denom) + ac * (vc * denom);
		}

		// 破綻防止: 全バリアント同時適用時にボディメッシュへのめり込みを爪単位で補正
		private static void CorrectDeltasForBodyPenetration(
			Vector3[] basePositions,
			List<(string shapeName, Vector3[] dv, Vector3[] dn, Vector3[] dt,
				string? leftName, string? rightName, bool hasAnyDelta)> deltas,
			SkinnedMeshRenderer bodySmr,
			(string Name, Transform?[] VariantNails, string? LeftName, string? RightName)[] variants,
			Matrix4x4 combinedGoW2L,
			int[] nailVertexOffsets,
			int nailCount)
		{
			if (bodySmr.sharedMesh == null) return;

			Mesh bodyMesh = bodySmr.sharedMesh;
			int vertCount = basePositions.Length;

			// ボディメッシュのBlendShape重みを保存
			float[] savedWeights = new float[bodyMesh.blendShapeCount];
			for (int i = 0; i < savedWeights.Length; i++)
				savedWeights[i] = bodySmr.GetBlendShapeWeight(i);

			// 全バリアントのBlendShapeを100に設定（最悪ケース）
			foreach (var variant in variants)
			{
				int idx = bodyMesh.GetBlendShapeIndex(variant.Name);
				if (idx >= 0) bodySmr.SetBlendShapeWeight(idx, 100f);
				if (!string.IsNullOrEmpty(variant.LeftName))
				{
					int lidx = bodyMesh.GetBlendShapeIndex(variant.LeftName);
					if (lidx >= 0) bodySmr.SetBlendShapeWeight(lidx, 100f);
				}
				if (!string.IsNullOrEmpty(variant.RightName))
				{
					int ridx = bodyMesh.GetBlendShapeIndex(variant.RightName);
					if (ridx >= 0) bodySmr.SetBlendShapeWeight(ridx, 100f);
				}
			}

			// ボディメッシュをベイク
			Mesh bakedBody = new Mesh();
			bodySmr.BakeMesh(bakedBody);

			// BlendShape重みを復元
			for (int i = 0; i < savedWeights.Length; i++)
				bodySmr.SetBlendShapeWeight(i, savedWeights[i]);

			// ボディ頂点を結合メッシュのローカル空間に変換
			Matrix4x4 bodyToLocal = combinedGoW2L * bodySmr.transform.localToWorldMatrix;
			Vector3[] bodyVerts = bakedBody.vertices;
			for (int i = 0; i < bodyVerts.Length; i++)
				bodyVerts[i] = bodyToLocal.MultiplyPoint3x4(bodyVerts[i]);

			int[] bodyTris = bakedBody.triangles;

			// 全バリアントのデルタを合算
			Vector3[] combinedDelta = new Vector3[vertCount];
			for (int di = 0; di < deltas.Count; di++)
				for (int vi = 0; vi < vertCount; vi++)
					combinedDelta[vi] += deltas[di].dv[vi];

			// ネイル頂点のバウンディングボックスを計算（ボディ三角形フィルタリング用）
			Vector3 nailMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
			Vector3 nailMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
			bool hasAnyNonZero = false;
			for (int vi = 0; vi < vertCount; vi++)
			{
				if (combinedDelta[vi].sqrMagnitude < 1e-8f) continue;
				hasAnyNonZero = true;
				Vector3 pos = basePositions[vi] + combinedDelta[vi];
				nailMin = Vector3.Min(nailMin, pos);
				nailMax = Vector3.Max(nailMax, pos);
			}

			if (!hasAnyNonZero)
			{
				UnityEngine.Object.DestroyImmediate(bakedBody);
				return;
			}

			// ベースポジションもバウンディングボックスに含める
			for (int vi = 0; vi < vertCount; vi++)
			{
				if (combinedDelta[vi].sqrMagnitude < 1e-8f) continue;
				nailMin = Vector3.Min(nailMin, basePositions[vi]);
				nailMax = Vector3.Max(nailMax, basePositions[vi]);
			}

			float margin = 0.05f;
			nailMin -= Vector3.one * margin;
			nailMax += Vector3.one * margin;

			// ネイル付近のボディ三角形をフィルタリング（AABB交差判定）
			var nearbyTris = new List<int>();
			for (int ti = 0; ti < bodyTris.Length; ti += 3)
			{
				Vector3 a = bodyVerts[bodyTris[ti]];
				Vector3 b = bodyVerts[bodyTris[ti + 1]];
				Vector3 c = bodyVerts[bodyTris[ti + 2]];

				Vector3 triMin = Vector3.Min(a, Vector3.Min(b, c));
				Vector3 triMax = Vector3.Max(a, Vector3.Max(b, c));

				if (triMax.x >= nailMin.x && triMin.x <= nailMax.x &&
					triMax.y >= nailMin.y && triMin.y <= nailMax.y &&
					triMax.z >= nailMin.z && triMin.z <= nailMax.z)
				{
					nearbyTris.Add(bodyTris[ti]);
					nearbyTris.Add(bodyTris[ti + 1]);
					nearbyTris.Add(bodyTris[ti + 2]);
				}
			}

			if (nearbyTris.Count == 0)
			{
					UnityEngine.Object.DestroyImmediate(bakedBody);
				return;
			}

			int[] nearbyTriArray = nearbyTris.ToArray();
			int totalCorrected = 0;

			// 爪ごとに個別の均一補正を適用（形状保持）
			for (int ni = 0; ni < nailCount; ni++)
			{
				int nailStart = nailVertexOffsets[ni];
				int nailEnd = (ni + 1 < nailCount) ? nailVertexOffsets[ni + 1] : basePositions.Length;

				Vector3 nailCorrSum = Vector3.zero;
				int nailCorrCount = 0;

				for (int vi = nailStart; vi < nailEnd; vi++)
				{
					// 全バリアントのデルタを合算した予測位置を計算
					Vector3 combined = Vector3.zero;
					for (int di = 0; di < deltas.Count; di++)
						combined += deltas[di].dv[vi];
					if (combined.sqrMagnitude < 1e-8f) continue;

					Vector3 predictedPos = basePositions[vi] + combined;

					// ボディメッシュ上の最近傍点を探す
					float minDistSq = float.MaxValue;
					Vector3 nearestPoint = Vector3.zero;
					Vector3 nearestNormal = Vector3.zero;

					for (int ti = 0; ti < nearbyTriArray.Length; ti += 3)
					{
						Vector3 a = bodyVerts[nearbyTriArray[ti]];
						Vector3 b = bodyVerts[nearbyTriArray[ti + 1]];
						Vector3 c = bodyVerts[nearbyTriArray[ti + 2]];
						Vector3 closest = ClosestPointOnTriangle(predictedPos, a, b, c);
						float distSq = (predictedPos - closest).sqrMagnitude;
						if (distSq < minDistSq)
						{
							minDistSq = distSq;
							nearestPoint = closest;
							Vector3 cn = Vector3.Cross(b - a, c - a);
							float cnMag = cn.magnitude;
							nearestNormal = cnMag > 1e-10f ? cn / cnMag : Vector3.zero;
						}
					}

					if (nearestNormal.sqrMagnitude < 0.01f) continue;

					float signedDist = Vector3.Dot(predictedPos - nearestPoint, nearestNormal);
					float pushMargin = 0.0005f;
					if (signedDist < pushMargin)
					{
						float pushAmount = pushMargin - signedDist + 0.0003f;
						nailCorrSum += nearestNormal * pushAmount;
						nailCorrCount++;
					}
				}

				if (nailCorrCount == 0) continue;

				// この爪の平均補正を爪内の全頂点に均一適用
				Vector3 nailCorr = nailCorrSum / nailCorrCount;
				for (int vi = nailStart; vi < nailEnd; vi++)
				{
					int contributors = 0;
					for (int di = 0; di < deltas.Count; di++)
						if (deltas[di].dv[vi].sqrMagnitude > 1e-8f) contributors++;
					if (contributors > 0)
					{
						Vector3 perVariant = nailCorr / contributors;
						for (int di = 0; di < deltas.Count; di++)
						{
							if (deltas[di].dv[vi].sqrMagnitude > 1e-8f)
								deltas[di].dv[vi] += perVariant;
						}
					}
				}
				totalCorrected += nailCorrCount;
			}

			UnityEngine.Object.DestroyImmediate(bakedBody);
		}

		public static GameObject? BakeAndCombineNailMeshes(
			Transform?[] nailObjects,
			GameObject nailPrefabObject,
			string zoneName,
			string saveBasePath,
			(string Name, Transform?[] VariantNails, string? LeftName, string? RightName)[]? variants = null,
			bool[]? isLeftSide = null,
			SkinnedMeshRenderer? bodySmr = null)
		{
			var indexedNails = nailObjects
				.Select((t, i) => (t, originalIndex: i))
				.Where(x => x.t != null && x.t.GetComponent<SkinnedMeshRenderer>() != null
				            && x.t.GetComponent<SkinnedMeshRenderer>()!.sharedMesh != null)
				.ToArray();

			var validPairs = indexedNails
				.Select(x => (transform: x.t!, smr: x.t!.GetComponent<SkinnedMeshRenderer>()!))
				.ToArray();

			bool[]? validPairsIsLeft = isLeftSide != null
				? indexedNails.Select(x => x.originalIndex < isLeftSide.Length && isLeftSide[x.originalIndex]).ToArray()
				: null;
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
				Material[] mats = validPairs[si].smr.sharedMaterials;
				if (mats.Length == 0) continue;

				Mesh mesh = validPairs[si].smr.sharedMesh;
				int vOff = vertexOffsets[si];

				for (int matIdx = 0; matIdx < mats.Length; matIdx++)
				{
					Material mat = mats[matIdx];
					if (mat == null) continue;

					if (!materialGroups.ContainsKey(mat))
					{
						materialGroups[mat] = new List<int>();
						materialList.Add(mat);
					}

					// メッシュのサブメッシュ数以上のマテリアルはサブメッシュ0のジオメトリを使用（オーバーレイ）
					int subMeshIdx = matIdx < mesh.subMeshCount ? matIdx : 0;
					int[] srcTris = mesh.GetTriangles(subMeshIdx);
					for (int ti = 0; ti < srcTris.Length; ti++)
					{
						materialGroups[mat].Add(srcTris[ti] + vOff);
					}
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
				// デルタ情報を収集（体めり込み補正のため、BlendShapeFrame追加を遅延）
				var collectedDeltas = new List<(string shapeName, Vector3[] dv, Vector3[] dn, Vector3[] dt,
					string? leftName, string? rightName, bool hasAnyDelta)>();

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

					bool hasAnyDelta = false;

					int vOff = 0;
					for (int si = 0; si < validPairs.Length; si++)
					{
						Transform baseNail = validPairs[si].transform;
						Mesh baseMesh = validPairs[si].smr.sharedMesh;
						int siVertCount = baseMesh.vertexCount;

						// Step 1: 名前完全一致
						Transform? variantNail = variant.VariantNails.FirstOrDefault(t => t != null && t.name == baseNail.name);
						// Step 2: 大文字小文字無視
						if (variantNail == null)
							variantNail = variant.VariantNails.FirstOrDefault(t => t != null && string.Equals(t.name, baseNail.name, System.StringComparison.OrdinalIgnoreCase));
						// Step 3: インデックスでフォールバック（同じ指の位置にあるネイルを使用）
						if (variantNail == null && si < variant.VariantNails.Length && variant.VariantNails[si] != null)
						{
							variantNail = variant.VariantNails[si];
							Debug.Log($"[MDNailTool] BakeAndCombine: '{shapeName}' '{baseNail.name}' 名前不一致 → インデックス {si} のバリアント '{variantNail!.name}' を使用");
						}

						if (variantNail == null)
						{
							ToolConsole.Log($"BakeAndCombine: '{shapeName}' バリアントに '{baseNail.name}' に一致するネイルが見つかりません（スキップ）");
							vOff += siVertCount;
							continue;
						}
						else
						{
						SkinnedMeshRenderer? varSmr = variantNail.GetComponent<SkinnedMeshRenderer>();
						if (varSmr == null || varSmr.sharedMesh == null)
						{
							ToolConsole.Log($"BakeAndCombine: '{baseNail.name}' のバリアントに SkinnedMeshRenderer またはメッシュがありません");
						}
						else if (varSmr.sharedMesh.vertexCount != siVertCount)
						{
							ToolConsole.Log($"BakeAndCombine: base='{baseNail.name}' vertCount={siVertCount}, variant vertCount={varSmr.sharedMesh.vertexCount} → MISMATCH");
						}
						if (varSmr != null && varSmr.sharedMesh != null && varSmr.sharedMesh.vertexCount == siVertCount)
						{
							hasAnyDelta = true;

							Mesh bakedBaseMesh = new Mesh();
							validPairs[si].smr.BakeMesh(bakedBaseMesh);

							Mesh bakedVarMesh = new Mesh();
							varSmr.BakeMesh(bakedVarMesh);

							Matrix4x4 variantToLocal = combinedGoW2L * variantNail.localToWorldMatrix;
							Matrix4x4 baseToLocal = combinedGoW2L * baseNail.localToWorldMatrix;

							Vector3[] varVerts2 = bakedVarMesh.vertices;
							Vector3[] baseVerts2 = bakedBaseMesh.vertices;
							Vector3[] varNormals2 = bakedVarMesh.normals;
							Vector3[] baseNormals2 = bakedBaseMesh.normals;

							for (int vi = 0; vi < siVertCount; vi++)
							{
								Vector3 vv = variantToLocal.MultiplyPoint3x4(varVerts2[vi]);
								Vector3 bv = baseToLocal.MultiplyPoint3x4(baseVerts2[vi]);
								fullDv[vOff + vi] = vv - bv;

								Vector3 vn = varNormals2.Length > vi ? varNormals2[vi] : Vector3.up;
								Vector3 bn = baseNormals2.Length > vi ? baseNormals2[vi] : Vector3.up;
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


					collectedDeltas.Add((shapeName, fullDv, fullDn, fullDt,
						variant.LeftName, variant.RightName, hasAnyDelta));
				}

				// 複数バリアント同時適用時の体めり込み補正
				if (bodySmr != null && collectedDeltas.Count > 1)
				{
					Vector3[] basePositions = combinedMesh.vertices;
					CorrectDeltasForBodyPenetration(basePositions, collectedDeltas, bodySmr, variants, combinedGoW2L, vertexOffsets, validPairs.Length);
				}

				// 収集したデルタからBlendShapeFrameを追加
				foreach (var (shapeName2, fullDv2, fullDn2, fullDt2, leftName, rightName, hasAnyDelta2) in collectedDeltas)
				{
					if (!string.IsNullOrEmpty(leftName) && !string.IsNullOrEmpty(rightName) && validPairsIsLeft != null)
					{
						var leftDv = new Vector3[totalVertCount];
						var leftDn = new Vector3[totalVertCount];
						var leftDt = new Vector3[totalVertCount];
						var rightDv = new Vector3[totalVertCount];
						var rightDn = new Vector3[totalVertCount];
						var rightDt = new Vector3[totalVertCount];

						for (int si2 = 0; si2 < validPairs.Length; si2++)
						{
							int siVerts = validPairs[si2].smr.sharedMesh.vertexCount;
							int off = vertexOffsets[si2];
							var targetDv = validPairsIsLeft[si2] ? leftDv : rightDv;
							var targetDn = validPairsIsLeft[si2] ? leftDn : rightDn;
							var targetDt = validPairsIsLeft[si2] ? leftDt : rightDt;
							System.Array.Copy(fullDv2, off, targetDv, off, siVerts);
							System.Array.Copy(fullDn2, off, targetDn, off, siVerts);
							System.Array.Copy(fullDt2, off, targetDt, off, siVerts);
						}

						combinedMesh.AddBlendShapeFrame(leftName, 100f, leftDv, leftDn, leftDt);
						combinedMesh.AddBlendShapeFrame(rightName, 100f, rightDv, rightDn, rightDt);
						if (!hasAnyDelta2)
						{
							ToolConsole.Log($"BakeAndCombine: variant='{shapeName2}' L/R分割 デルタなし → ゼロデルタで生成");
						}
					}
					else
					{
						combinedMesh.AddBlendShapeFrame(shapeName2, 100f, fullDv2, fullDn2, fullDt2);
						if (!hasAnyDelta2)
						{
							ToolConsole.Log($"BakeAndCombine: variant='{shapeName2}' デルタなし → ゼロデルタで生成");
						}
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
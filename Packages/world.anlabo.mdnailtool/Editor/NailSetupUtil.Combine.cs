#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace world.anlabo.mdnailtool.Editor
{
	public static partial class NailSetupUtil
	{
		public static GameObject? BakeAndCombineNailMeshes(
			Transform?[] nailObjects,
			GameObject nailPrefabObject,
			string zoneName,
			string saveBasePath,
			(string Name, Transform?[] VariantNails, string? LeftName, string? RightName)[]? variants = null,
			bool[]? isLeftSide = null,
			SkinnedMeshRenderer? bodySmr = null,
			(string BSName, ShrinkBSScope Scope)[]? shrinkBSDefinitions = null)
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
			Undo.RegisterCreatedObjectUndo(combinedGo, "Nail Setup");
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
			var allTangents = new List<Vector4>();
			var allUVs      = new List<Vector2>();
			var allWeights  = new List<BoneWeight>();
			int[] vertexOffsets = new int[validPairs.Length];
			Mesh[] cachedMeshes = new Mesh[validPairs.Length];
			int vertexOffset = 0;

			Matrix4x4 combinedGoW2L = combinedGo.transform.worldToLocalMatrix;

			for (int si = 0; si < validPairs.Length; si++)
			{
				vertexOffsets[si] = vertexOffset;
				Mesh mesh = validPairs[si].smr.sharedMesh;
				cachedMeshes[si] = mesh;

				Mesh bakedMesh = new Mesh();
				validPairs[si].smr.BakeMesh(bakedMesh);

				Matrix4x4 toLocal = combinedGoW2L * validPairs[si].transform.localToWorldMatrix;

				Vector3[] srcVerts   = bakedMesh.vertices;
				Vector3[] srcNormals = bakedMesh.normals;
				Vector4[] tangents   = mesh.tangents;
				for (int vi = 0; vi < mesh.vertexCount; vi++)
				{
					allVerts.Add(toLocal.MultiplyPoint3x4(srcVerts[vi]));
					Vector3 n = srcNormals.Length > vi ? srcNormals[vi] : Vector3.up;
					allNormals.Add(toLocal.MultiplyVector(n).normalized);

					Vector4 tangent = tangents.Length > vi ? tangents[vi] : new Vector4(1f, 0f, 0f, 1f);
					Vector3 tangentDir = toLocal.MultiplyVector(new Vector3(tangent.x, tangent.y, tangent.z)).normalized;
					allTangents.Add(new Vector4(tangentDir.x, tangentDir.y, tangentDir.z, tangent.w));
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
			combinedMesh.tangents    = allTangents.ToArray();
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

					// メッシュのサブメッシュ数以上のマテリアルはサブメッシュ0のジオメトリを使用 (オーバーレイ)
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
							variantNail = variant.VariantNails.FirstOrDefault(t => t != null && string.Equals(t.name, baseNail.name, StringComparison.OrdinalIgnoreCase));
						// Step 3: インデックスでフォールバック
						if (variantNail == null && si < variant.VariantNails.Length && variant.VariantNails[si] != null)
						{
							variantNail = variant.VariantNails[si];
							ToolConsole.Log($"BakeAndCombine: '{shapeName}' '{baseNail.name}' 名前不一致 -> インデックス {si} のバリアント '{variantNail!.name}' を使用");
						}

						if (variantNail == null)
						{
							ToolConsole.Log($"BakeAndCombine: '{shapeName}' バリアントに '{baseNail.name}' に一致するネイルが見つかりません (スキップ)");
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
								ToolConsole.Log($"BakeAndCombine: base='{baseNail.name}' vertCount={siVertCount}, variant vertCount={varSmr.sharedMesh.vertexCount} -> MISMATCH");
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
							ToolConsole.Log($"BakeAndCombine: variant='{shapeName2}' L/R分割 デルタなし -> ゼロデルタで生成");
						}
					}
					else
					{
						combinedMesh.AddBlendShapeFrame(shapeName2, 100f, fullDv2, fullDn2, fullDt2);
						if (!hasAnyDelta2)
						{
							ToolConsole.Log($"BakeAndCombine: variant='{shapeName2}' デルタなし -> ゼロデルタで生成");
						}
					}
				}
			}

			// Shrink連動: アバター本体Shrink_*BSに同名sync用、該当箇所の頂点を原点に集めるBSを動的注入
			if (shrinkBSDefinitions != null && shrinkBSDefinitions.Length > 0)
			{
				Vector3[] basePositions = combinedMesh.vertices;
				Vector3[] zeroNormals = new Vector3[totalVertCount];
				Vector3[] zeroTangents = new Vector3[totalVertCount];

				foreach (var (bsName, scope) in shrinkBSDefinitions)
				{
					if (combinedMesh.GetBlendShapeIndex(bsName) >= 0)
					{
						ToolConsole.Log($"[Warning] BakeAndCombine: Shrink BS '{bsName}' は既存BSと衝突するためスキップ");
						continue;
					}

					Vector3[] dv = new Vector3[totalVertCount];
					for (int si = 0; si < validPairs.Length; si++)
					{
						int siVerts = validPairs[si].smr.sharedMesh.vertexCount;
						int off = vertexOffsets[si];

						bool isLeft = validPairsIsLeft != null && si < validPairsIsLeft.Length && validPairsIsLeft[si];

						bool shouldShrink = scope switch
						{
							ShrinkBSScope.All => true,
							ShrinkBSScope.LeftOnly => isLeft,
							ShrinkBSScope.RightOnly => !isLeft,
							_ => false
						};

						if (!shouldShrink) continue;

						for (int vi = 0; vi < siVerts; vi++)
						{
							dv[off + vi] = -basePositions[off + vi];
						}
					}

					combinedMesh.AddBlendShapeFrame(bsName, 100f, dv, zeroNormals, zeroTangents);
					ToolConsole.Log($"BakeAndCombine: Shrink BS '{bsName}' (scope={scope}) を注入");
				}
			}

			if (!Directory.Exists(saveBasePath))
				Directory.CreateDirectory(saveBasePath);

			// blendshape 追加後の bind pose vertices から localBounds を再計算
			// variants なしで作られた Combined SMR が frustum culling で消える問題の対策
			combinedMesh.RecalculateBounds();

			string assetPath = $"{saveBasePath}/{zoneName}.asset";
			Mesh? existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
			// 既存assetのインスタンスIDを保持し、再着用時に他オブジェクトからの参照を維持する
			// 頂点数が変わると CopySerialized が旧頂点を残し点線混在する (issue #495). 一致時のみ ID 保持コピー.
			if (existingMesh != null && existingMesh.vertexCount == combinedMesh.vertexCount)
			{
				EditorUtility.CopySerialized(combinedMesh, existingMesh);
				combinedMesh = existingMesh;
			}
			else if (existingMesh != null)
			{
				// 頂点数変更時. DeleteAsset は ID が変わり OFF 複数着用で Missing 化するため全データを手で書き写し ID を保持する (issue #495).
				CopyMeshContents(combinedMesh, existingMesh);
				combinedMesh = existingMesh;
			}
			else
			{
				AssetDatabase.CreateAsset(combinedMesh, assetPath);
			}
			// Reapply after asset identity-preserving copy paths so existing mesh assets keep tangents.
			combinedMesh.tangents = allTangents.ToArray();
			AssetDatabase.SaveAssets();

			SkinnedMeshRenderer combinedSmr = combinedGo.AddComponent<SkinnedMeshRenderer>();
			combinedSmr.bones           = boneTransforms;
			combinedSmr.rootBone        = boneTransforms[0];
			combinedSmr.sharedMaterials = materialList.ToArray();
			combinedSmr.sharedMesh      = MDNailToolAssetLoader.LoadAssetSafe<Mesh>(assetPath);

			for (int bsIdx = 0; bsIdx < combinedMesh.blendShapeCount; bsIdx++)
			{
				combinedSmr.SetBlendShapeWeight(bsIdx, 0f);
			}

			foreach (var (t, _) in validPairs)
				UnityEngine.Object.DestroyImmediate(t.gameObject);

			return combinedGo;
		}

		// src の全データを dst へ書き写す. asset の ID を保持したまま頂点数を変えるために使う (issue #495).
		private static void CopyMeshContents(Mesh src, Mesh dst)
		{
			dst.Clear();
			dst.indexFormat = src.indexFormat;
			dst.vertices    = src.vertices;
			dst.normals     = src.normals;
			dst.tangents    = src.tangents;
			dst.uv          = src.uv;
			dst.boneWeights = src.boneWeights;
			dst.bindposes   = src.bindposes;

			dst.subMeshCount = src.subMeshCount;
			for (int i = 0; i < src.subMeshCount; i++)
				dst.SetTriangles(src.GetTriangles(i), i);

			for (int bsIdx = 0; bsIdx < src.blendShapeCount; bsIdx++)
			{
				string bsName = src.GetBlendShapeName(bsIdx);
				int frames = src.GetBlendShapeFrameCount(bsIdx);
				for (int f = 0; f < frames; f++)
				{
					var dv = new Vector3[src.vertexCount];
					var dn = new Vector3[src.vertexCount];
					var dt = new Vector3[src.vertexCount];
					float w = src.GetBlendShapeFrameWeight(bsIdx, f);
					src.GetBlendShapeFrameVertices(bsIdx, f, dv, dn, dt);
					dst.AddBlendShapeFrame(bsName, w, dv, dn, dt);
				}
			}

			dst.RecalculateBounds();
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

			float[] savedWeights = new float[bodyMesh.blendShapeCount];
			for (int i = 0; i < savedWeights.Length; i++)
				savedWeights[i] = bodySmr.GetBlendShapeWeight(i);

			// 全バリアントのBlendShapeを100に設定 (最悪ケース)
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

			Mesh bakedBody = new Mesh();
			bodySmr.BakeMesh(bakedBody);

			for (int i = 0; i < savedWeights.Length; i++)
				bodySmr.SetBlendShapeWeight(i, savedWeights[i]);

			Matrix4x4 bodyToLocal = combinedGoW2L * bodySmr.transform.localToWorldMatrix;
			Vector3[] bodyVerts = bakedBody.vertices;
			for (int i = 0; i < bodyVerts.Length; i++)
				bodyVerts[i] = bodyToLocal.MultiplyPoint3x4(bodyVerts[i]);

			int[] bodyTris = bakedBody.triangles;

			Vector3[] combinedDelta = new Vector3[vertCount];
			for (int di = 0; di < deltas.Count; di++)
				for (int vi = 0; vi < vertCount; vi++)
					combinedDelta[vi] += deltas[di].dv[vi];

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

			for (int vi = 0; vi < vertCount; vi++)
			{
				if (combinedDelta[vi].sqrMagnitude < 1e-8f) continue;
				nailMin = Vector3.Min(nailMin, basePositions[vi]);
				nailMax = Vector3.Max(nailMax, basePositions[vi]);
			}

			float margin = 0.05f;
			nailMin -= Vector3.one * margin;
			nailMax += Vector3.one * margin;

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

			for (int ni = 0; ni < nailCount; ni++)
			{
				int nailStart = nailVertexOffsets[ni];
				int nailEnd = (ni + 1 < nailCount) ? nailVertexOffsets[ni + 1] : basePositions.Length;

				Vector3 nailCorrSum = Vector3.zero;
				int nailCorrCount = 0;

				for (int vi = nailStart; vi < nailEnd; vi++)
				{
					Vector3 combined = Vector3.zero;
					for (int di = 0; di < deltas.Count; di++)
						combined += deltas[di].dv[vi];
					if (combined.sqrMagnitude < 1e-8f) continue;

					Vector3 predictedPos = basePositions[vi] + combined;

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
	}
}

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
			(string BSName, bool[] NailMask)[]? shrinkBSDefinitions = null,
			bool enablePenetrationCorrection = false,
			SkinnedMeshRenderer? penetrationCorrectionBodySmr = null,
			bool[]? transferBodyWeightsByNail = null)
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
			bool[]? validPairsTransferBodyWeights = transferBodyWeightsByNail != null
				? indexedNails.Select(x => x.originalIndex < transferBodyWeightsByNail.Length && transferBodyWeightsByNail[x.originalIndex]).ToArray()
				: null;
			if (validPairs.Length == 0) return null;

			GameObject combinedGo = new GameObject(zoneName);
			Undo.RegisterCreatedObjectUndo(combinedGo, "Nail Setup");
			combinedGo.transform.SetParent(nailPrefabObject.transform, false);
			combinedGo.transform.localPosition = Vector3.zero;
			combinedGo.transform.localRotation = Quaternion.identity;
			combinedGo.transform.localScale    = Vector3.one;

			var rigidBoneTransforms = new Transform[validPairs.Length];
			for (int i = 0; i < validPairs.Length; i++)
			{
				rigidBoneTransforms[i] = validPairs[i].transform.parent;
			}

			bool transferBodyWeights = bodySmr != null
				&& bodySmr.sharedMesh != null
				&& bodySmr.sharedMesh.boneWeights.Length == bodySmr.sharedMesh.vertexCount
				&& bodySmr.bones.Length > 0
				&& rigidBoneTransforms.All(b => Array.IndexOf(bodySmr.bones, b) >= 0);
			Transform[] boneTransforms = rigidBoneTransforms;

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

				Matrix4x4 toLocal = combinedGoW2L * validPairs[si].transform.localToWorldMatrix;

				Vector3[] srcVerts;
				Vector3[] srcNormals;
				if (transferBodyWeights)
				{
					// mode=1のみ、骨変形とTransform Scaleの二重適用を避ける。
					GetLocalBlendShapeDeformedData(validPairs[si].smr, out srcVerts, out srcNormals);
				}
				else
				{
					// mode=0/未指定は既存アバターの従来経路を維持する。
					Mesh bakedMesh = new Mesh();
					validPairs[si].smr.BakeMesh(bakedMesh);
					srcVerts = bakedMesh.vertices;
					srcNormals = bakedMesh.normals;
					UnityEngine.Object.DestroyImmediate(bakedMesh);
				}
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

								Vector3[] baseVerts2;
								Vector3[] baseNormals2;
								Vector3[] varVerts2;
								Vector3[] varNormals2;
								if (transferBodyWeights)
								{
									GetLocalBlendShapeDeformedData(validPairs[si].smr, out baseVerts2, out baseNormals2);
									GetLocalBlendShapeDeformedData(varSmr, out varVerts2, out varNormals2);
								}
								else
								{
									Mesh bakedBaseMesh = new Mesh();
									validPairs[si].smr.BakeMesh(bakedBaseMesh);
									baseVerts2 = bakedBaseMesh.vertices;
									baseNormals2 = bakedBaseMesh.normals;
									Mesh bakedVarMesh = new Mesh();
									varSmr.BakeMesh(bakedVarMesh);
									varVerts2 = bakedVarMesh.vertices;
									varNormals2 = bakedVarMesh.normals;
									UnityEngine.Object.DestroyImmediate(bakedBaseMesh);
									UnityEngine.Object.DestroyImmediate(bakedVarMesh);
								}

								Matrix4x4 variantToLocal = combinedGoW2L * variantNail.localToWorldMatrix;
								Matrix4x4 baseToLocal = combinedGoW2L * baseNail.localToWorldMatrix;

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

							}
						}

						vOff += siVertCount;
					}

					collectedDeltas.Add((shapeName, fullDv, fullDn, fullDt,
						variant.LeftName, variant.RightName, hasAnyDelta));
				}

				// 複数バリアント同時適用時の体めり込み補正
				if (enablePenetrationCorrection && penetrationCorrectionBodySmr != null && collectedDeltas.Count > 1)
				{
					Vector3[] basePositions = combinedMesh.vertices;
					CorrectDeltasForBodyPenetration(basePositions, collectedDeltas, penetrationCorrectionBodySmr, variants, combinedGoW2L, vertexOffsets, validPairs.Length);
				}

				foreach (var (shapeName2, fullDv2, fullDn2, fullDt2, leftName, rightName, hasAnyDelta2) in collectedDeltas)
				{
					if ((!string.IsNullOrEmpty(leftName) || !string.IsNullOrEmpty(rightName)) && validPairsIsLeft != null)
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

						if (combinedMesh.GetBlendShapeIndex(shapeName2) < 0) combinedMesh.AddBlendShapeFrame(shapeName2, 100f, fullDv2, fullDn2, fullDt2);
						if (!string.IsNullOrEmpty(leftName) && combinedMesh.GetBlendShapeIndex(leftName) < 0) combinedMesh.AddBlendShapeFrame(leftName, 100f, leftDv, leftDn, leftDt);
						if (!string.IsNullOrEmpty(rightName) && combinedMesh.GetBlendShapeIndex(rightName) < 0) combinedMesh.AddBlendShapeFrame(rightName, 100f, rightDv, rightDn, rightDt);
						if (!hasAnyDelta2)
						{
							ToolConsole.Log($"BakeAndCombine: variant='{shapeName2}' L/R分割 デルタなし -> ゼロデルタで生成");
						}
					}
					else
					{
						if (combinedMesh.GetBlendShapeIndex(shapeName2) < 0) combinedMesh.AddBlendShapeFrame(shapeName2, 100f, fullDv2, fullDn2, fullDt2);
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

				foreach (var (bsName, nailMask) in shrinkBSDefinitions)
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

						int originalIndex = indexedNails[si].originalIndex;
						bool shouldShrink = nailMask != null
							&& originalIndex >= 0
							&& originalIndex < nailMask.Length
							&& nailMask[originalIndex];

						if (!shouldShrink) continue;

						for (int vi = 0; vi < siVerts; vi++)
						{
							dv[off + vi] = -basePositions[off + vi];
						}
					}

					combinedMesh.AddBlendShapeFrame(bsName, 100f, dv, zeroNormals, zeroTangents);
					ToolConsole.Log($"BakeAndCombine: Shrink BS '{bsName}' (targets={nailMask.Count(x => x)}) を注入");
				}
			}

			if (!Directory.Exists(saveBasePath))
				Directory.CreateDirectory(saveBasePath);

			// 通常形状だけでなく Point 等の全 BlendShape 形状を含む Bounds を作る。
			// 着用時に一度だけ計算し、実行中の追加負荷は発生させない。
			// 着脱用 Shrink BS は頂点を原点へ潰すため、Bounds に含めない。
			// Point 等の実際のネイル形状だけを包含する。
			IEnumerable<string>? boundsExcludedBlendShapes = shrinkBSDefinitions?.Select(x => x.BSName);
			combinedMesh.bounds = CalculateBlendShapeBounds(combinedMesh, 0.01f, boundsExcludedBlendShapes);

			string assetPath = $"{saveBasePath}/{zoneName}.asset";
			Mesh? existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
			// 既存assetのインスタンスIDを保持し、再着用時に他オブジェクトからの参照を維持する。
			// CopySerialized は同頂点数でも頂点バッファが古く残るケースがあるため、常に明示コピーする。
			if (existingMesh != null)
			{
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
			// 両手/両足を 1 Renderer にまとめるため、Bounds の基準は
			// 指ボーンではなく安定した Hips (取得できない場合はアバタールート) にする。
			Animator? animator = nailPrefabObject.GetComponentInParent<Animator>();
			Transform avatarRoot = animator != null ? animator.transform : nailPrefabObject.transform;
			Transform? hips = animator != null && animator.isHuman
				? animator.GetBoneTransform(HumanBodyBones.Hips)
				: null;
			combinedSmr.rootBone = hips != null ? hips : avatarRoot;
			combinedSmr.sharedMaterials = materialList.ToArray();
			combinedSmr.sharedMesh      = combinedMesh;
			Matrix4x4 meshToBoundsRoot = combinedSmr.rootBone.worldToLocalMatrix * combinedGo.transform.localToWorldMatrix;
			Bounds nailBounds = TransformBounds(combinedMesh.bounds, meshToBoundsRoot);
			// アバター原点から高さ 2m を基本範囲とし、実ネイル形状が外へ出る場合だけ拡張する。
			// 極端なしゃがみ・寝姿勢も考慮し、下方 0.5m まで余裕を持たせる。
			Bounds avatarSafetyBounds = new Bounds(new Vector3(0f, 0.75f, 0f), Vector3.one * 2.5f);
			Matrix4x4 avatarToBoundsRoot = combinedSmr.rootBone.worldToLocalMatrix * avatarRoot.localToWorldMatrix;
			Bounds localBounds = TransformBounds(avatarSafetyBounds, avatarToBoundsRoot);
			localBounds.Encapsulate(nailBounds.min);
			localBounds.Encapsulate(nailBounds.max);
			combinedSmr.localBounds = localBounds;

			// 結合済みSMRをBakeしてからBody表面のウェイトを転写する。頂点とTransformは変更しない。
			if (transferBodyWeights)
			{
				bool[]? transferVertexMask = null;
				if (validPairsTransferBodyWeights != null)
				{
					transferVertexMask = new bool[combinedMesh.vertexCount];
					for (int si = 0; si < validPairs.Length; si++)
					{
						bool shouldTransfer = si < validPairsTransferBodyWeights.Length && validPairsTransferBodyWeights[si];
						for (int vi = 0; vi < cachedMeshes[si].vertexCount; vi++)
							transferVertexMask[vertexOffsets[si] + vi] = shouldTransfer;
					}
				}
				ApplySurfaceWeightTransfer(combinedSmr, bodySmr!, transferVertexMask);
				combinedSmr.sharedMesh = null;
				combinedSmr.sharedMesh = combinedMesh;
				EditorUtility.SetDirty(combinedMesh);
				AssetDatabase.SaveAssets();
			}

			for (int bsIdx = 0; bsIdx < combinedMesh.blendShapeCount; bsIdx++)
			{
				combinedSmr.SetBlendShapeWeight(bsIdx, 0f);
			}

			foreach (var (t, _) in validPairs)
				UnityEngine.Object.DestroyImmediate(t.gameObject);

			return combinedGo;
		}

		private static Bounds CalculateBlendShapeBounds(Mesh mesh, float padding, IEnumerable<string>? excludedBlendShapes = null)
		{
			Vector3[] baseVertices = mesh.vertices;
			if (baseVertices.Length == 0) return new Bounds(Vector3.zero, Vector3.one * padding * 2f);
			HashSet<string> excluded = excludedBlendShapes != null
				? new HashSet<string>(excludedBlendShapes)
				: new HashSet<string>();

			Bounds bounds = new Bounds(baseVertices[0], Vector3.zero);
			for (int i = 1; i < baseVertices.Length; i++) bounds.Encapsulate(baseVertices[i]);

			var deltaVertices = new Vector3[baseVertices.Length];
			var deltaNormals = new Vector3[baseVertices.Length];
			var deltaTangents = new Vector3[baseVertices.Length];
			for (int shape = 0; shape < mesh.blendShapeCount; shape++)
			{
				if (excluded.Contains(mesh.GetBlendShapeName(shape))) continue;
				int frameCount = mesh.GetBlendShapeFrameCount(shape);
				for (int frame = 0; frame < frameCount; frame++)
				{
					mesh.GetBlendShapeFrameVertices(shape, frame, deltaVertices, deltaNormals, deltaTangents);
					for (int vertex = 0; vertex < baseVertices.Length; vertex++)
						bounds.Encapsulate(baseVertices[vertex] + deltaVertices[vertex]);
				}
			}

			bounds.Expand(padding * 2f);
			return bounds;
		}

		private static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
		{
			Vector3 min = source.min;
			Vector3 max = source.max;
			Bounds transformed = new Bounds(matrix.MultiplyPoint3x4(min), Vector3.zero);
			for (int x = 0; x < 2; x++)
			for (int y = 0; y < 2; y++)
			for (int z = 0; z < 2; z++)
			{
				Vector3 corner = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
				transformed.Encapsulate(matrix.MultiplyPoint3x4(corner));
			}
			return transformed;
		}

		private static void GetLocalBlendShapeDeformedData(SkinnedMeshRenderer smr, out Vector3[] vertices, out Vector3[] normals)
		{
			Mesh mesh = smr.sharedMesh;
			vertices = (Vector3[])mesh.vertices.Clone();
			normals = mesh.normals.Length == mesh.vertexCount
				? (Vector3[])mesh.normals.Clone()
				: Enumerable.Repeat(Vector3.up, mesh.vertexCount).ToArray();

			for (int shape = 0; shape < mesh.blendShapeCount; shape++)
			{
				float requestedWeight = smr.GetBlendShapeWeight(shape);
				if (Mathf.Abs(requestedWeight) <= 1e-6f) continue;
				int frameCount = mesh.GetBlendShapeFrameCount(shape);
				if (frameCount == 0) continue;

				int lower = -1;
				int upper = 0;
				for (int frame = 0; frame < frameCount; frame++)
				{
					float frameWeight = mesh.GetBlendShapeFrameWeight(shape, frame);
					if (frameWeight < requestedWeight) { lower = frame; upper = Mathf.Min(frame + 1, frameCount - 1); }
					else { upper = frame; break; }
				}

				var upperDv = new Vector3[mesh.vertexCount];
				var upperDn = new Vector3[mesh.vertexCount];
				var upperDt = new Vector3[mesh.vertexCount];
				mesh.GetBlendShapeFrameVertices(shape, upper, upperDv, upperDn, upperDt);
				float upperWeight = mesh.GetBlendShapeFrameWeight(shape, upper);

				Vector3[] lowerDv;
				Vector3[] lowerDn;
				float lowerWeight;
				if (lower >= 0 && lower != upper)
				{
					lowerDv = new Vector3[mesh.vertexCount];
					lowerDn = new Vector3[mesh.vertexCount];
					var lowerDt = new Vector3[mesh.vertexCount];
					mesh.GetBlendShapeFrameVertices(shape, lower, lowerDv, lowerDn, lowerDt);
					lowerWeight = mesh.GetBlendShapeFrameWeight(shape, lower);
				}
				else
				{
					lowerDv = new Vector3[mesh.vertexCount];
					lowerDn = new Vector3[mesh.vertexCount];
					lowerWeight = 0f;
				}

				float denominator = upperWeight - lowerWeight;
				float t = Mathf.Abs(denominator) > 1e-6f ? (requestedWeight - lowerWeight) / denominator : 1f;
				for (int i = 0; i < mesh.vertexCount; i++)
				{
					vertices[i] += Vector3.LerpUnclamped(lowerDv[i], upperDv[i], t);
					normals[i] = (normals[i] + Vector3.LerpUnclamped(lowerDn[i], upperDn[i], t)).normalized;
				}
			}
		}
		private static void ApplySurfaceWeightTransfer(SkinnedMeshRenderer nails, SkinnedMeshRenderer body, bool[]? transferVertexMask = null)
		{
			Mesh nailMesh = nails.sharedMesh;
			Mesh bodyMesh = body.sharedMesh;
			BoneWeight[] bodyWeights = bodyMesh.boneWeights;
			int[] bodyTriangles = bodyMesh.triangles;
			if (bodyWeights.Length != bodyMesh.vertexCount || bodyTriangles.Length < 3) return;
			if (bodyMesh.bindposes.Length != body.bones.Length) return;

			BoneWeight[] rigidWeights = nailMesh.boneWeights;
			Transform[] rigidBones = nails.bones;
			Vector3[] currentVertices = nailMesh.vertices;
			Vector3[] bindVertices = new Vector3[currentVertices.Length];
			Matrix4x4[] currentToBind = new Matrix4x4[currentVertices.Length];
			var output = new BoneWeight[currentVertices.Length];
			Vector3[] bodyBindVertices = bodyMesh.vertices;
			Matrix4x4 nailsToWorld = nails.transform.localToWorldMatrix;
			Matrix4x4 worldToNails = nails.transform.worldToLocalMatrix;
			Matrix4x4 bodyToWorld = body.transform.localToWorldMatrix;
			Matrix4x4 worldToBody = body.transform.worldToLocalMatrix;

			for (int vi = 0; vi < currentVertices.Length; vi++)
			{
				BoneWeight rigid = rigidWeights[vi];
				int rigidIndex = rigid.boneIndex0;
				if (rigidIndex < 0 || rigidIndex >= rigidBones.Length) return;
				Transform rigidBone = rigidBones[rigidIndex];
				int bodyBoneIndex = Array.IndexOf(body.bones, rigidBone);
				if (bodyBoneIndex < 0) return;

				// 現在のDistal骨上へ配置された点を、同じ骨のBody bind姿勢へ戻す。
				Matrix4x4 toBind = worldToNails * bodyToWorld
					* bodyMesh.bindposes[bodyBoneIndex].inverse
					* rigidBone.worldToLocalMatrix * nailsToWorld;
				currentToBind[vi] = toBind;
				bindVertices[vi] = toBind.MultiplyPoint3x4(currentVertices[vi]);
				Vector3 bindPointBody = worldToBody.MultiplyPoint3x4(
					nailsToWorld.MultiplyPoint3x4(bindVertices[vi]));

				float nearestDistance = float.MaxValue;
				int ia = 0, ib = 0, ic = 0;
				Vector3 bary = new Vector3(1f, 0f, 0f);
				for (int ti = 0; ti < bodyTriangles.Length; ti += 3)
				{
					int a = bodyTriangles[ti], b = bodyTriangles[ti + 1], c = bodyTriangles[ti + 2];
					Vector3 closest = ClosestPointAndBarycentricOnTriangle(bindPointBody, bodyBindVertices[a], bodyBindVertices[b], bodyBindVertices[c], out Vector3 candidateBary);
					float distance = (bindPointBody - closest).sqrMagnitude;
					if (distance >= nearestDistance) continue;
					nearestDistance = distance;
					ia = a; ib = b; ic = c;
					bary = candidateBary;
				}

				BoneWeight weight = default;
				if (transferVertexMask == null || (vi < transferVertexMask.Length && transferVertexMask[vi]))
				{
					var accumulated = new Dictionary<int, float>();
					AccumulateBoneWeight(accumulated, bodyWeights[ia], bary.x);
					AccumulateBoneWeight(accumulated, bodyWeights[ib], bary.y);
					AccumulateBoneWeight(accumulated, bodyWeights[ic], bary.z);
					var strongest = accumulated.Where(x => x.Value > 0f).OrderByDescending(x => x.Value).Take(4).ToArray();
					float total = strongest.Sum(x => x.Value);
					for (int i = 0; total > 1e-8f && i < strongest.Length; i++)
					{
						float normalized = strongest[i].Value / total;
						if (i == 0) { weight.boneIndex0 = strongest[i].Key; weight.weight0 = normalized; }
						else if (i == 1) { weight.boneIndex1 = strongest[i].Key; weight.weight1 = normalized; }
						else if (i == 2) { weight.boneIndex2 = strongest[i].Key; weight.weight2 = normalized; }
						else { weight.boneIndex3 = strongest[i].Key; weight.weight3 = normalized; }
					}
				}
				else
				{
					// 非対象の爪は元の親Distalボーンへ100%追従させる。
					weight.boneIndex0 = bodyBoneIndex;
					weight.weight0 = 1f;
				}
				output[vi] = weight;
			}

			TransformMeshToBindPose(nailMesh, bindVertices, currentToBind);
			Matrix4x4 bodyToNails = worldToBody * nails.transform.localToWorldMatrix;
			Matrix4x4[] nailBindposes = bodyMesh.bindposes.Select(bindpose => bindpose * bodyToNails).ToArray();
			nailMesh.boneWeights = output;
			nailMesh.bindposes = nailBindposes;
			nails.bones = body.bones;
			nails.rootBone = body.rootBone;
			nails.sharedMesh = null;
			nails.sharedMesh = nailMesh;		}

		private static void TransformMeshToBindPose(Mesh mesh, Vector3[] bindVertices, Matrix4x4[] currentToBind)
		{
			Vector3[] sourceNormals = mesh.normals;
			Vector4[] sourceTangents = mesh.tangents;
			var bindNormals = new Vector3[bindVertices.Length];
			var bindTangents = new Vector4[bindVertices.Length];
			for (int i = 0; i < bindVertices.Length; i++)
			{
				Matrix4x4 normalMatrix = currentToBind[i].inverse.transpose;
				Vector3 normal = sourceNormals.Length > i ? sourceNormals[i] : Vector3.up;
				bindNormals[i] = normalMatrix.MultiplyVector(normal).normalized;
				Vector4 tangent = sourceTangents.Length > i ? sourceTangents[i] : new Vector4(1f, 0f, 0f, 1f);
				Vector3 tangent3 = currentToBind[i].MultiplyVector(new Vector3(tangent.x, tangent.y, tangent.z)).normalized;
				bindTangents[i] = new Vector4(tangent3.x, tangent3.y, tangent3.z, tangent.w);
			}

			var frames = new List<(string name, float weight, Vector3[] dv, Vector3[] dn, Vector3[] dt)>();
			for (int shape = 0; shape < mesh.blendShapeCount; shape++)
			{
				for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++)
				{
					var dv = new Vector3[bindVertices.Length];
					var dn = new Vector3[bindVertices.Length];
					var dt = new Vector3[bindVertices.Length];
					mesh.GetBlendShapeFrameVertices(shape, frame, dv, dn, dt);
					for (int i = 0; i < bindVertices.Length; i++)
					{
						dv[i] = currentToBind[i].MultiplyVector(dv[i]);
						dn[i] = currentToBind[i].inverse.transpose.MultiplyVector(dn[i]);
						dt[i] = currentToBind[i].MultiplyVector(dt[i]);
					}
					frames.Add((mesh.GetBlendShapeName(shape), mesh.GetBlendShapeFrameWeight(shape, frame), dv, dn, dt));
				}
			}

			mesh.vertices = bindVertices;
			mesh.normals = bindNormals;
			mesh.tangents = bindTangents;
			mesh.ClearBlendShapes();
			foreach (var frame in frames)
				mesh.AddBlendShapeFrame(frame.name, frame.weight, frame.dv, frame.dn, frame.dt);
			mesh.RecalculateBounds();
		}
		private static Vector3 ClosestPointAndBarycentricOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 barycentric)
		{
			Vector3 ab = b - a, ac = c - a, ap = p - a;
			float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
			if (d1 <= 0f && d2 <= 0f) { barycentric = new Vector3(1f, 0f, 0f); return a; }
			Vector3 bp = p - b;
			float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
			if (d3 >= 0f && d4 <= d3) { barycentric = new Vector3(0f, 1f, 0f); return b; }
			float vc = d1 * d4 - d3 * d2;
			if (vc <= 0f && d1 >= 0f && d3 <= 0f) { float v = d1 / (d1 - d3); barycentric = new Vector3(1f - v, v, 0f); return a + v * ab; }
			Vector3 cp = p - c;
			float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
			if (d6 >= 0f && d5 <= d6) { barycentric = new Vector3(0f, 0f, 1f); return c; }
			float vb = d5 * d2 - d1 * d6;
			if (vb <= 0f && d2 >= 0f && d6 <= 0f) { float w = d2 / (d2 - d6); barycentric = new Vector3(1f - w, 0f, w); return a + w * ac; }
			float va = d3 * d6 - d5 * d4;
			if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) { float w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); barycentric = new Vector3(0f, 1f - w, w); return b + w * (c - b); }
			float denominator = 1f / (va + vb + vc);
			float insideV = vb * denominator, insideW = vc * denominator, insideU = 1f - insideV - insideW;
			barycentric = new Vector3(insideU, insideV, insideW);
			return insideU * a + insideV * b + insideW * c;
		}

		private static void AccumulateBoneWeight(Dictionary<int, float> result, BoneWeight weight, float scale)
		{
			Add(weight.boneIndex0, weight.weight0 * scale);
			Add(weight.boneIndex1, weight.weight1 * scale);
			Add(weight.boneIndex2, weight.weight2 * scale);
			Add(weight.boneIndex3, weight.weight3 * scale);
			void Add(int bone, float value)
			{
				if (value <= 0f) return;
				result[bone] = result.TryGetValue(bone, out float current) ? current + value : value;
			}
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

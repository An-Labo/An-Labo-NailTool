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

namespace world.anlabo.mdnailtool.Editor {
	/// <summary>
	/// ネイルのメッシュ置換やマテリアル適用を行うユーティリティクラスです。
	/// </summary>
	public static class NailSetupUtil {
		
		/// <summary>
		/// ハンドネイルのメッシュを置き換えます。
		/// </summary>
		public static void ReplaceHandsNailMesh(Transform?[] handsNailObjects, Mesh?[] overrideMesh) {
			if (overrideMesh.Length != 10) {
				throw new ArgumentException($"Incorrect length of {nameof(overrideMesh)} parameter : {overrideMesh.Length}");
			}

			if (handsNailObjects.Length != 10) {
				throw new ArgumentException($"Incorrect length of {nameof(handsNailObjects)} parameter : {overrideMesh.Length}");
			}
			
			ReplaceMesh(handsNailObjects, overrideMesh);
		}

		/// <summary>
		/// フットネイルのメッシュを置き換えます。
		/// </summary>
		public static void ReplaceFootNailMesh(Transform?[] leftFootNailObjects, Transform?[] rightFootNailObjects, string nailShape) {
			if (leftFootNailObjects.Length != 5) {
				throw new ArgumentException($"Incorrect length of {nameof(leftFootNailObjects)} parameter : {leftFootNailObjects.Length}");
			}
			
			if (rightFootNailObjects.Length != 5) {
				throw new ArgumentException($"Incorrect length of {nameof(rightFootNailObjects)} parameter : {rightFootNailObjects.Length}");
			}

			using DBNailShape db = new();
			NailShape? shape = db.FindNailShapeByName(nailShape);
			if (shape == null) {
				throw new ArgumentException("Not found nail shape.");
			}

			string? path = null;
			foreach (string guid in shape.FootFbxFolderGUID) {
				if (string.IsNullOrEmpty(guid)) continue;
				path = AssetDatabase.GUIDToAssetPath(guid);
				if (string.IsNullOrEmpty(path)) continue;
				if (Directory.Exists(path)) break;
			}

			if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) {
				throw new InvalidOperationException("Not found foot nail objects.");
			}
			

			Mesh[] leftFootOverrideMesh;
			Mesh[] rightFootOverrideMesh;
			if (File.Exists($"{path}/{shape.FootFbxNamePrefix}{MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST[0]}.fbx")) {
				leftFootOverrideMesh = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName}.fbx")
					.Select(AssetDatabase.LoadAssetAtPath<Mesh>)
					.ToArray();

				rightFootOverrideMesh = MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName}.fbx")
					.Select(AssetDatabase.LoadAssetAtPath<Mesh>)
					.ToArray();
			} else {
				leftFootOverrideMesh = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName.Replace('.', '_')}.fbx")
					.Select(AssetDatabase.LoadAssetAtPath<Mesh>)
					.ToArray();

				rightFootOverrideMesh = MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST
					.Select(objectName => $"{path}/{shape.FootFbxNamePrefix}{objectName.Replace('.', '_')}.fbx")
					.Select(name => { Debug.Log(name);
						return name;
					})
					.Select(AssetDatabase.LoadAssetAtPath<Mesh>)
					.ToArray();
				
			}

			ReplaceMesh(leftFootNailObjects, leftFootOverrideMesh);
			ReplaceMesh(rightFootNailObjects, rightFootOverrideMesh);
		}

		private static void ReplaceMesh(Transform?[] transforms, Mesh?[] overrideMesh) {
			for (int index = 0; index < overrideMesh.Length; index++) {
				Mesh? newMesh = overrideMesh[index];
				if (newMesh == null) continue;
				Transform? targetTransform = transforms[index];
				if (targetTransform == null) continue;
				
				SkinnedMeshRenderer? smr = targetTransform.GetComponent<SkinnedMeshRenderer>();
				if (smr == null) continue;

				Dictionary<string, float> savedWeights = new();
				Mesh currentMesh = smr.sharedMesh;
				if (currentMesh != null) {
					for (int i = 0; i < currentMesh.blendShapeCount; i++) {
						savedWeights[currentMesh.GetBlendShapeName(i)] = smr.GetBlendShapeWeight(i);
					}
				}

				smr.sharedMesh = newMesh;

				foreach (var weightInfo in savedWeights) {
					int newIndex = newMesh.GetBlendShapeIndex(weightInfo.Key);
					if (newIndex != -1) {
						smr.SetBlendShapeWeight(newIndex, weightInfo.Value);
					}
				}
			}
		}

		/// <summary>
		/// ネイルのマテリアルを適用します。
		/// 以前は足の処理が一括適用されていましたが、指ごとに個別適用するように修正されました。
		/// </summary>
		public static void ReplaceNailMaterial(Transform?[] handsNailObjects, IEnumerable<Transform?> leftFootNailObjects, IEnumerable<Transform?> rightFootNailObjects,
			(INailProcessor, string, string)[] nailDesignAndVariationNames, string nailShapeName, bool isGenerate, bool isPreview) {
			
			if (nailDesignAndVariationNames.Length != 20) {
				throw new ArgumentException($"Incorrect length of {nameof(nailDesignAndVariationNames)} parameter : {nailDesignAndVariationNames.Length}");
			}

			// --- ハンドネイルの適用 (Index 0-9) ---
			for (int index = 0; index < handsNailObjects.Length; index++) {
				(INailProcessor processor, string materialName, string colorName) = nailDesignAndVariationNames[index];

				Transform? transform = handsNailObjects[index];
				if (transform == null) {
					// プレビュー等でオブジェクトが存在しない場合はログを出してスキップ
					// Debug.LogError($"{nameof(handsNailObjects)}[{index}] is null.");
					continue;
				}

				ApplyMaterial(transform, processor, materialName, colorName, nailShapeName, isGenerate, isPreview);
			}

			// --- 左足ネイルの適用 (Index 10-14) ---
			// IEnumerableを配列に変換してインデックスアクセスできるようにする
			var leftFootArray = leftFootNailObjects.ToArray();
			for (int i = 0; i < leftFootArray.Length; i++)
			{
				int designIndex = 10 + i; // 左足の開始インデックスは10
				if (designIndex >= nailDesignAndVariationNames.Length) break;

				(INailProcessor processor, string materialName, string colorName) = nailDesignAndVariationNames[designIndex];
				Transform? transform = leftFootArray[i];
				
				if (transform == null) continue;
				ApplyMaterial(transform, processor, materialName, colorName, nailShapeName, isGenerate, isPreview);
			}

			// --- 右足ネイルの適用 (Index 15-19) ---
			var rightFootArray = rightFootNailObjects.ToArray();
			for (int i = 0; i < rightFootArray.Length; i++)
			{
				int designIndex = 15 + i; // 右足の開始インデックスは15
				if (designIndex >= nailDesignAndVariationNames.Length) break;

				(INailProcessor processor, string materialName, string colorName) = nailDesignAndVariationNames[designIndex];
				Transform? transform = rightFootArray[i];

				if (transform == null) continue;
				ApplyMaterial(transform, processor, materialName, colorName, nailShapeName, isGenerate, isPreview);
			}
		}

		/// <summary>
		/// 個別のTransformに対してマテリアルを適用するヘルパーメソッド
		/// </summary>
		private static void ApplyMaterial(Transform transform, INailProcessor processor, string materialName, string colorName, string nailShapeName, bool isGenerate, bool isPreview)
		{
			Renderer? renderer = transform.GetComponent<Renderer>();
			if (renderer == null) {
				Debug.LogError($"Not found Renderer : {transform.name}");
				return;
			}
			
			// デザイン情報がnull（空データ）の場合は適用しない
			if (processor == null) return;

			Material mainMaterial = processor.GetMaterial(materialName, colorName, nailShapeName, isGenerate, isPreview);
			IEnumerable<Material> additionalMaterial = processor.GetAdditionalMaterials(colorName, nailShapeName, isPreview);
			renderer.sharedMaterials = additionalMaterial.Prepend(mainMaterial).ToArray();
		}

		public static void AttachAdditionalObjects(Transform?[] handsNailObjects, (INailProcessor, string, string)[] nailDesignAndVariationNames, string nailShapeName, bool isPreview) {
			if (handsNailObjects.Length != 10) {
				throw new ArgumentException($"Incorrect length of {nameof(handsNailObjects)} parameter : {handsNailObjects.Length}");
			}

			for (int index = 0; index < handsNailObjects.Length; index++) {
				(INailProcessor processor, string _, string colorName) = nailDesignAndVariationNames[index];

				Transform? transform = handsNailObjects[index];
				if (transform == null) {
					// Debug.LogError($"{nameof(handsNailObjects)}[{index}] is null.");
					continue;
				}
				
				if (processor == null) continue;

				foreach (Transform additionalObject in processor.GetAdditionalObjects(colorName, nailShapeName, (MDNailToolDefines.TargetFinger)index, isPreview)) {
					additionalObject.SetParent(transform, false);
				}
			}
		}
	}
}
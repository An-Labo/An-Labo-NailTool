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
	public static class NailSetupUtil {
		public static void ReplaceHandsNailMesh(Transform?[] handsNailObjects, Mesh?[] overrideMesh) {
			if (overrideMesh.Length != 10) {
				throw new ArgumentException($"Incorrect length of {nameof(overrideMesh)} parameter : {overrideMesh.Length}");
			}

			if (handsNailObjects.Length != 10) {
				throw new ArgumentException($"Incorrect length of {nameof(handsNailObjects)} parameter : {overrideMesh.Length}");
			}
			
			ReplaceMesh(handsNailObjects, overrideMesh);
		}

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
				Mesh? mesh = overrideMesh[index];
				if (mesh == null) continue;
				Transform? targetTransform = transforms[index];
				if (targetTransform == null) continue;
				SkinnedMeshRenderer? skinnedMeshRenderer = targetTransform.GetComponent<SkinnedMeshRenderer>();
				if (skinnedMeshRenderer == null) continue;
				skinnedMeshRenderer.sharedMesh = mesh;
			}
		}

		public static void ReplaceNailMaterial(Transform?[] handsNailObjects, IEnumerable<Transform?> leftFootNailObjects, IEnumerable<Transform?> rightFootNailObjects,
			(INailProcessor, string, string)[] nailDesignAndVariationNames, string nailShapeName, bool isGenerate, bool isPreview) {
			if (nailDesignAndVariationNames.Length != 12) {
				throw new ArgumentException($"Incorrect length of {nameof(nailDesignAndVariationNames)} parameter : {nailDesignAndVariationNames.Length}");
			}

			for (int index = 0; index < handsNailObjects.Length; index++) {
				(INailProcessor processor, string materialName, string colorName) = nailDesignAndVariationNames[index];

				Transform? transform = handsNailObjects[index];
				if (transform == null) {
					Debug.LogError($"{nameof(handsNailObjects)}[{index}] is null.");
					continue;
				}

				Renderer? renderer = transform.GetComponent<Renderer>();
				if (renderer == null) {
					Debug.LogError($"Not found Renderer : {transform.name}");
					continue;
				}

				Material mainMaterial = processor.GetMaterial(materialName, colorName, nailShapeName, isGenerate, isPreview);
				IEnumerable<Material> additionalMaterial = processor.GetAdditionalMaterials(colorName, nailShapeName, isPreview);
				renderer.sharedMaterials = additionalMaterial.Prepend(mainMaterial).ToArray();
			}

			{
				(INailProcessor processor, string materialName, string colorName) = nailDesignAndVariationNames[(int)MDNailToolDefines.TargetFingerAndToe.LeftToes];

				foreach (Transform? transform in leftFootNailObjects) {
					if (transform == null) continue;
					Renderer? renderer = transform.GetComponent<Renderer>();
					if (renderer == null) {
						Debug.LogError($"Not found Renderer : {transform.name}");
						continue;
					}

					Material mainMaterial = processor.GetMaterial(materialName, colorName, nailShapeName, isGenerate, isPreview);
					IEnumerable<Material> additionalMaterial = processor.GetAdditionalMaterials(colorName, nailShapeName, isPreview);
					renderer.sharedMaterials = additionalMaterial.Prepend(mainMaterial).ToArray();
				}
			}

			{
				(INailProcessor processor, string materialName, string colorName) = nailDesignAndVariationNames[(int)MDNailToolDefines.TargetFingerAndToe.RightToes];

				foreach (Transform? transform in rightFootNailObjects) {
					if (transform == null) continue;
					Renderer? renderer = transform.GetComponent<Renderer>();
					if (renderer == null) {
						Debug.LogError($"Not found Renderer : {transform.name}");
						continue;
					}

					Material mainMaterial = processor.GetMaterial(materialName, colorName, nailShapeName, isGenerate, isPreview);
					IEnumerable<Material> additionalMaterial = processor.GetAdditionalMaterials(colorName, nailShapeName, isPreview);
					renderer.sharedMaterials = additionalMaterial.Prepend(mainMaterial).ToArray();
				}
			}
		}

		public static void AttachAdditionalObjects(Transform?[] handsNailObjects, (INailProcessor, string, string)[] nailDesignAndVariationNames, string nailShapeName, bool isPreview) {
			if (handsNailObjects.Length != 10) {
				throw new ArgumentException($"Incorrect length of {nameof(handsNailObjects)} parameter : {handsNailObjects.Length}");
			}

			for (int index = 0; index < handsNailObjects.Length; index++) {
				(INailProcessor processor, string _, string colorName) = nailDesignAndVariationNames[index];

				Transform? transform = handsNailObjects[index];
				if (transform == null) {
					Debug.LogError($"{nameof(handsNailObjects)}[{index}] is null.");
					continue;
				}

				foreach (Transform additionalObject in processor.GetAdditionalObjects(colorName, nailShapeName, (MDNailToolDefines.TargetFinger)index, isPreview)) {
					additionalObject.SetParent(transform, false);
				}
			}
		}
	}
}
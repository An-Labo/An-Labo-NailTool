#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

			for (int index = 0; index < overrideMesh.Length; index++) {
				Mesh? mesh = overrideMesh[index];
				if (mesh == null) continue;
				Transform? targetTransform = handsNailObjects[index];
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
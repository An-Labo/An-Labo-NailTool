#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.NailDesigns;

namespace world.anlabo.mdnailtool.Editor
{
	public static partial class NailSetupUtil
	{
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
				throw new NailToolDeveloperException("NailSetup", $"Incorrect length of {nameof(nailDesignAndVariationNames)} parameter : {nailDesignAndVariationNames.Length}");
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
				ToolConsole.Error("NailSetup", $"Not found Renderer : {transform.name}");
				return;
			}

			// 装着しない指 (チェックOFF) はプレビュー側で Renderer 無効化してチップ非表示。
			// Apply 時は NailSetupProcessor で先に Destroy 済みなので、この経路は通らない。
			if (processor == null)
			{
				renderer.enabled = false;
				return;
			}
			renderer.enabled = true;

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
			if (handsNailObjects.Length != 10)
			{
				throw new NailToolDeveloperException("NailSetup", $"Incorrect length of {nameof(handsNailObjects)} parameter : {handsNailObjects.Length}");
			}

			for (int index = 0; index < handsNailObjects.Length; index++)
			{
				Transform? transform = handsNailObjects[index];

				IEnumerable<Transform>? fingerObjects = perFingerAdditionalObjects != null && index < perFingerAdditionalObjects.Length
					? perFingerAdditionalObjects[index] : null;

				if (transform == null)
				{
					// 親付け先がない場合は Instantiate 済み孤児 GO を Destroy する (Scene 残留防止).
					if (fingerObjects != null)
					{
						foreach (Transform additionalObject in fingerObjects)
						{
							if (additionalObject != null) UnityEngine.Object.DestroyImmediate(additionalObject.gameObject);
						}
					}
					continue;
				}

				if (fingerObjects != null)
				{
					foreach (Transform additionalObject in fingerObjects)
					{
						additionalObject.SetParent(transform, false);
					}
				}
				else
				{
					// フォールバック: processor から取得
					(INailProcessor processor, string _, string colorName) = nailDesignAndVariationNames[index];
					if (processor == null)
					{
						continue;
					}

					foreach (Transform additionalObject in processor.GetAdditionalObjects(colorName, nailShapeName, (MDNailToolDefines.TargetFinger)index, isPreview))
					{
						additionalObject.SetParent(transform, false);
					}
				}
			}
		}

		// mipmapEnabled=false のテクスチャはスキップ (ミップマップ自体がないため).
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
	}
}

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
						NailSetupTransaction.TrackCreated(additionalObject.gameObject);
						additionalObject.SetParent(transform, false);
						PlaceNailTipAnchor(additionalObject, transform);
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
						NailSetupTransaction.TrackCreated(additionalObject.gameObject);
						additionalObject.SetParent(transform, false);
						PlaceNailTipAnchor(additionalObject, transform);
					}
				}
			}
		}

		// 形ごとに爪先の位置が違うため、目印の子を爪メッシュの決まった頂点へ合わせる。
		private static void PlaceNailTipAnchor(Transform additionalObject, Transform nail)
		{
			Transform? anchor = additionalObject.Find("NailTipAnchor");
			if (anchor == null) return;
			// 形によって爪を長さ方向だけ伸ばしてあるので、チェーンの輪が伸びないよう縦横の縮尺をそろえる。
			Vector3 nailScale = nail.localScale;
			if (nailScale.y != 0f && nailScale.z != 0f)
				additionalObject.localScale = new Vector3(1f, nailScale.x / nailScale.y, nailScale.x / nailScale.z);
			SkinnedMeshRenderer? smr = nail.GetComponent<SkinnedMeshRenderer>();
			if (smr == null || smr.sharedMesh == null) return;
			if (!NailTipVertexTable.TryGet(smr.sharedMesh.name, out int index)) return;
			Mesh baked = new Mesh();
			smr.BakeMesh(baked);
			Vector3[] vertices = baked.vertices;
			UnityEngine.Object.DestroyImmediate(baked);
			if (index < 0 || index >= vertices.Length) return;

			// 角の頂点は爪の反りで中心より低いので、中心線上(表の頂点と同じ左右位置)だけから選ぶ。
			int tip = index;
			int crown = index;
			for (int i = 0; i < vertices.Length; i++)
			{
				if (Mathf.Abs(vertices[i].x - vertices[index].x) >= 0.0004f) continue;
				if (vertices[i].y > vertices[tip].y) tip = i;
				if (vertices[i].z > vertices[crown].z) crown = i;
			}

			// 金属が爪を貫く所を爪先の端の頂点から次の頂点へ1/3の所に置き、チェーンを爪先の傾きにそろえる。
			Vector3 tipWorld = smr.transform.TransformPoint(vertices[tip]);
			Vector3 nextWorld = smr.transform.TransformPoint(vertices[index]);
			anchor.position = Vector3.Lerp(tipWorld, nextWorld, 1f / 3f);
			Vector3 outward = tipWorld - nextWorld;
			Transform frame = anchor.parent;
			if (outward.sqrMagnitude > 1e-12f) anchor.rotation = Quaternion.FromToRotation(frame.up, outward.normalized) * frame.rotation;

			// 爪の縁に当たり判定を置くため、もう1つの目印を爪の中心線上の一番先の頂点へ合わせる。
			Transform? edge = anchor.Find("NailTipEdge");
			if (edge == null) return;
			Vector3 edgeLocal = anchor.InverseTransformPoint(smr.transform.TransformPoint(vertices[tip]));
			Vector3 crownLocal = anchor.InverseTransformPoint(smr.transform.TransformPoint(vertices[crown]));
			edgeLocal.x = 0f;
			crownLocal.x = 0f;
			edge.localPosition = edgeLocal;
			// 当たり判定の上面を、爪先の端から爪の一番高い所へ向けて爪の表に沿わせる。
			Vector3 along = edgeLocal - crownLocal;
			if (along.sqrMagnitude > 1e-12f) edge.localRotation = Quaternion.FromToRotation(Vector3.up, along.normalized);
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

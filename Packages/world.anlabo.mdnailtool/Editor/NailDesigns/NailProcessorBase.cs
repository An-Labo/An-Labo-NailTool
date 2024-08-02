using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.JsonData;
using Object = UnityEngine.Object;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.NailDesigns {
	public abstract class NailProcessorBase : INailProcessor {
		public string DesignName { get; }
		public DesignData DesignData { get; }

		protected NailProcessorBase(string designName, DesignData? designData = null) {
			this.DesignName = designName;
			this.DesignData = designData ?? INailProcessor.GetDesignData(designName);
		}

		public Material GetMaterial(string materialName, string colorName, string nailShapeName, bool isGenerate, bool isPreview) {
			if (!isGenerate && !isPreview) {
				Material targetMaterial = this.GetBaseMaterial(materialName, nailShapeName);
				this.ProcessMaterial(targetMaterial, materialName, colorName, nailShapeName);
				return targetMaterial;
			}

			// マテリアル生成がON || プレビューの場合新規マテリアルを生成する。
			string materialKey = this.GetMaterialKey(materialName, colorName, nailShapeName, isPreview);

			// キャッシュされたマテリアルがあればそれを反す
			Material? cashedMaterial = isPreview ? INailProcessor.GetPreviewMaterialCash(materialKey) : INailProcessor.GetCreatedMaterialCash(materialKey);
			if (cashedMaterial != null) return cashedMaterial;

			Material baseMaterial = this.GetBaseMaterial(materialName, nailShapeName);
			Material clonedMaterial = Object.Instantiate(baseMaterial);
			
			this.ProcessMaterial(clonedMaterial, materialName, colorName, nailShapeName);

			if (isPreview) {
				// プレビュー用の場合ファイルとしては保存しない
				INailProcessor.RegisterPreviewMaterialCash(materialKey, clonedMaterial);
			} else {
				// 
				if (!Directory.Exists(MDNailToolDefines.GENERATED_ASSET_PATH)) {
					Directory.CreateDirectory(MDNailToolDefines.GENERATED_ASSET_PATH);
				}

				AssetDatabase.CreateAsset(clonedMaterial, $"{MDNailToolDefines.GENERATED_ASSET_PATH}generated_{DateTime.Now : yyyy-MM-dd-HH-mm-ss}_{materialKey}.mat");
				AssetDatabase.Refresh();
				INailProcessor.RegisterCreatedMaterialCash(materialKey, clonedMaterial);
			}


			return clonedMaterial;
		}


		public virtual IEnumerable<Material> GetAdditionalMaterials(string colorName, string nailShapeName, bool isPreview) {
			return Enumerable.Empty<Material>();
		}

		public virtual IEnumerable<Transform> GetAdditionalObjects(string colorName, string nailShapeName, MDNailToolDefines.TargetFinger targetFinger, bool isPreview) {
			return Enumerable.Empty<Transform>();
		}

		public virtual void ReportDesign(StringBuilder builder) { }

		public virtual void ReportVariation(string materialName, string variationName, StringBuilder builder) { }


		protected virtual string GetMaterialKey(string materialName, string variationName, string nailShapeName, bool isPreview) {
			string materialKey = $"{this.DesignName}.{materialName}.{variationName}.{nailShapeName}";
			if (isPreview) {
				materialKey += ".preview";
			}

			return materialKey;
		}

		protected abstract Material GetBaseMaterial(string materialName, string nailShapeName);
		protected abstract void ProcessMaterial(Material targetMaterial, string materialName, string colorName, string nailShapeName);
		public abstract bool IsInstalledMaterialVariation(string materialName);
		public abstract bool IsInstalledColorVariation(string materialName, string colorName);
		public abstract bool IsSupportedNailShape(string shapeName);
	}
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Core;
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
			if (!this.IsInstalledColorVariation(materialName, colorName)) {
				Debug.LogWarning($"[MDNailTool] Skipped: '{this.DesignName}' (material='{materialName}', color='{colorName}', shape='{nailShapeName}') is not installed. Returning fallback material.");
				return this.GetFallbackMaterial();
			}

			try {
				string? presetName = GlobalSetting.SelectedShaderPreset;
				Material? preset = string.IsNullOrEmpty(presetName) ? null : ShaderPresetScanner.FindPresetByName(presetName!);
				bool hasPreset = preset != null;

				if (!isGenerate && !isPreview && !hasPreset) {
					Material targetMaterial = this.GetBaseMaterial(materialName, nailShapeName);
					this.ProcessMaterial(targetMaterial, materialName, colorName, nailShapeName);
					return targetMaterial;
				}

				string materialKey = this.GetMaterialKey(materialName, colorName, nailShapeName, isPreview);

				Material? cashedMaterial = isPreview ? INailProcessor.GetPreviewMaterialCash(materialKey) : INailProcessor.GetCreatedMaterialCash(materialKey);
				if (cashedMaterial != null) return cashedMaterial;

				Material baseMaterial = this.GetBaseMaterial(materialName, nailShapeName);
				Material clonedMaterial;
				if (hasPreset) {
					clonedMaterial = new Material(preset!);
					ShaderPresetApplier.OverrideFromNail(clonedMaterial, baseMaterial);
				} else {
					clonedMaterial = new Material(baseMaterial);
				}

				this.ProcessMaterial(clonedMaterial, materialName, colorName, nailShapeName);

				if (isPreview) {
					INailProcessor.RegisterPreviewMaterialCash(materialKey, clonedMaterial);
				} else {
					if (!Directory.Exists(MDNailToolDefines.GENERATED_ASSET_PATH)) {
						Directory.CreateDirectory(MDNailToolDefines.GENERATED_ASSET_PATH);
					}

					AssetDatabase.CreateAsset(clonedMaterial, $"{MDNailToolDefines.GENERATED_ASSET_PATH}generated_{DateTime.Now : yyyy-MM-dd-HH-mm-ss}_{materialKey}.mat");
					AssetDatabase.Refresh();
					INailProcessor.RegisterCreatedMaterialCash(materialKey, clonedMaterial);
				}

				return clonedMaterial;
			} catch (Exception ex) {
				Debug.LogWarning($"[MDNailTool] Failed to build material for '{this.DesignName}' (material='{materialName}', color='{colorName}', shape='{nailShapeName}'): {ex.Message}. Returning fallback material.");
				return this.GetFallbackMaterial();
			}
		}

		private Material GetFallbackMaterial() {
			Shader? shader = Shader.Find("Standard") ?? Shader.Find("Hidden/InternalErrorShader");
			return new Material(shader);
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
			string? presetName = GlobalSetting.SelectedShaderPreset;
			if (!string.IsNullOrEmpty(presetName)) {
				materialKey += $".preset_{SanitizeForFileName(presetName!)}";
			}
			if (isPreview) {
				materialKey += ".preview";
			}

			return materialKey;
		}

		// "User: 00_Face" のような ':' 等 Windows 不正文字を含む presetName を AssetDatabase.CreateAsset 用に正規化.
		private static string SanitizeForFileName(string s) {
			char[] invalid = Path.GetInvalidFileNameChars();
			var sb = new StringBuilder(s.Length);
			foreach (char c in s) {
				sb.Append(System.Array.IndexOf(invalid, c) >= 0 ? '_' : c);
			}
			return sb.ToString();
		}

		protected abstract Material GetBaseMaterial(string materialName, string nailShapeName);
		protected abstract void ProcessMaterial(Material targetMaterial, string materialName, string colorName, string nailShapeName);
		public abstract bool IsInstalledMaterialVariation(string materialName);
		public abstract bool IsInstalledColorVariation(string materialName, string colorName);
		public abstract bool IsSupportedNailShape(string shapeName);
	}
}
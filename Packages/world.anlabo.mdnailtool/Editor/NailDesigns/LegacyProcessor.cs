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
	public class LegacyProcessor : NailProcessorBase {
		private static readonly int MainTex = Shader.PropertyToID("_MainTex");

		public LegacyProcessor(string designName, DesignData? designData = null) : base(designName, designData) { }

		protected override Material GetBaseMaterial(string materialName, string nailShapeName) {
			string baseMaterialPath = this.GetMaterialPath(materialName, nailShapeName);
			Material? baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(baseMaterialPath);
			if (baseMaterial == null) throw new InvalidOperationException($"Not found base material. {this.DesignName} : {nailShapeName} : {baseMaterialPath}");
			return baseMaterial;
		}

		protected override void ProcessMaterial(Material targetMaterial, string materialName, string colorName, string nailShapeName) {
			string mainTexPath = this.GetTexturePath(materialName, colorName, nailShapeName);
			Texture2D? mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(mainTexPath);
			if (mainTex == null) throw new InvalidOperationException($"Not found Main texture. {this.DesignName} : {colorName} : {nailShapeName} : {mainTexPath}");
			targetMaterial.SetTexture(MainTex, mainTex);
		}

		public override IEnumerable<Material> GetAdditionalMaterials(string colorName, string nailShapeName, bool isPreview) {
			string[]? guids = this.DesignData.Legacy?.AdditionalMaterialGUIDs;
			if (guids == null) yield break;
			foreach (string guid in guids) {
				string materialPath = AssetDatabase.GUIDToAssetPath(guid);
				Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
				if (material == null) {
					Debug.LogError($"Not found additional material : {this.DesignName} : {colorName} : {guid} : {materialPath}");
					continue;
				}

				yield return material;
			}
		}

		public override IEnumerable<Transform> GetAdditionalObjects(string colorName, string nailShapeName, MDNailToolDefines.TargetFinger targetFinger, bool isPreview) {
			IEnumerable<string> guids = this.DesignData.Legacy?.AdditionalObjectGUIDs?.GetValueOrDefault(targetFinger) ?? Enumerable.Empty<string>();
			IEnumerable<string> allTargetGuids = this.DesignData.Legacy?.AdditionalObjectGUIDs?.GetValueOrDefault(MDNailToolDefines.TargetFinger.All) ?? Enumerable.Empty<string>();

			foreach (string guid in guids.Concat(allTargetGuids)) {
				string objectPath = AssetDatabase.GUIDToAssetPath(guid);
				GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(objectPath);
				if (obj == null) {
					Debug.LogError($"Not found additional object : {this.DesignName} : {colorName} : {targetFinger} : {guid} : {objectPath}");
					continue;
				}

				yield return Object.Instantiate(obj, Vector3.zero, Quaternion.identity).transform;
			}
		}

		public override void ReportDesign(StringBuilder builder) {
			builder.Append("  - RefDir : ");
			builder.AppendLine(this.GetDesignDirectoryPath());
		}

		public override void ReportVariation(string materialName, string colorName, StringBuilder builder) {
			builder.Append("    - BaseMatPath : ");
			builder.AppendLine(this.GetMaterialPath(materialName, "Natural"));
			builder.Append("    - TexPath : ");
			builder.AppendLine(this.GetTexturePath(materialName, colorName, "Natural"));
		}

		public override bool IsInstalledMaterialVariation(string materialName) {
			string subDirectoryPath = this.GetTextureSubDirectoryPath(materialName, "Natural");
			return Directory.Exists(subDirectoryPath);
		}

		public override bool IsInstalledColorVariation(string materialName, string colorName) {
			string mainTexturePath = this.GetTexturePath(materialName, colorName, "Natural");
			return File.Exists(mainTexturePath);
		}

		public override bool IsSupportedNailShape(string nailShapeName) {
			string nailShapeDirectoryPath = this.GetNailShapeDirectoryPath(nailShapeName);
			return Directory.Exists(nailShapeDirectoryPath);
		}

		private string GetDesignDirectoryPath() {
			string fixedPath = $"{MDNailToolDefines.LEGACY_DESIGN_PATH}【{this.DesignName}】";
			if (Directory.Exists(fixedPath)) {
				return fixedPath;
			}
			
			string? guid = this.DesignData.Legacy?.DesignDirectoryGUID;
			if (!string.IsNullOrEmpty(guid)) {
				string guidPath = AssetDatabase.GUIDToAssetPath(guid);
				if (!string.IsNullOrEmpty(guidPath) && Directory.Exists(guidPath)) {
					return guidPath;
				}
			}
			
			return fixedPath;
		}

		private string GetNailShapeDirectoryPath(string nailShapeName) {
			StringBuilder builder = new();
			builder.Append(this.GetDesignDirectoryPath());
			builder.Append('/');
			builder.Append("[Data]/[Texture]/[");
			builder.Append(nailShapeName);
			builder.Append("]/");
			return builder.ToString();
		}

		private string GetTextureSubDirectoryPath(string materialName, string nailShapeName) {
			string nailShapeDirectoryPath = this.GetNailShapeDirectoryPath(nailShapeName);
			StringBuilder builder = new();
			builder.Append(nailShapeDirectoryPath);
			if (string.IsNullOrEmpty(materialName)) return builder.ToString();

			builder.Append(materialName);
			builder.Append('/');
			return builder.ToString();
		}

		private string GetTexturePath(string materialName, string variationName, string nailShapeName) {
			string subDirectoryPath = this.GetTextureSubDirectoryPath(materialName, nailShapeName);
			StringBuilder builder = new();
			builder.Append(subDirectoryPath);
			builder.Append("[tex][");
			builder.Append(this.DesignName);
			builder.Append("][");
			builder.Append(nailShapeName);
			builder.Append(']');
			builder.Append(variationName);

			if (!string.IsNullOrEmpty(materialName)) {
				builder.Append(materialName);
			}

			builder.Append(".png");
			return builder.ToString();
		}

		private string GetMaterialPath(string materialName, string nailShapeName) {
			StringBuilder builder = new();
			builder.Append(MDNailToolDefines.NAIL_DESIGN_PATH);
			builder.Append(this.DesignName);
			builder.Append('/');
			if (!string.IsNullOrEmpty(materialName)) {
				builder.Append(materialName);
				builder.Append('/');
			}

			builder.Append("[mat][");
			builder.Append(this.DesignName);
			builder.Append("][lil-toon]");
			if (!string.IsNullOrEmpty(materialName)) {
				builder.Append(materialName);
				builder.Append('_');
			}
			builder.Append(nailShapeName);
			builder.Append(".mat");
			return builder.ToString();
		}
	}
}
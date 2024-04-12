using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.JsonData;
using Object = UnityEngine.Object;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.NailDesigns {
	public interface INailProcessor {
		
		public string DesignName { get; }
		public DesignData DesignData { get; }

		public Material GetMaterial(string materialName, string colorName, string nailShapeName, bool isGenerate, bool isPreview);
		public IEnumerable<Material> GetAdditionalMaterials(string colorName, string nailShapeName, bool isPreview);
		public IEnumerable<Transform> GetAdditionalObjects(string colorName, string nailShapeName, MDNailToolDefines.TargetFinger targetFinger, bool isPreview);
		public void ReportDesign(StringBuilder builder);
		public void ReportVariation(string materialName, string variationName,StringBuilder builder);

		bool IsInstalledMaterialVariation(string materialName);
		bool IsInstalledColorVariation(string materialName, string colorName);
		
		
		protected static DesignData GetDesignData(string designName) {
			string designPath = $"{MDNailToolDefines.NAIL_DESIGN_PATH}{designName}/";
			if (!Directory.Exists(designPath)) throw new InvalidOperationException($"Not found design directory : {designName} : {designPath}");
			string jsonPath = $"{designPath}_design.json";
			if (!File.Exists(jsonPath)) throw new InvalidOperationException($"Not found _design.json : {designName} : {designPath}");
			TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
			return DesignData.ToObject(textAsset.text);
		}

		public static NailProcessorBase CreateNailDesign(string designName) {
			DesignData designData = GetDesignData(designName);
			switch (designData.Type) {
				case DesignData.JsonType.Legacy:
					return new LegacyProcessor(designName, designData);
				case DesignData.JsonType.None:
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public static bool IsInstalledDesign(string? designName) {
			if (designName == null) return false;
			string designPath = $"{MDNailToolDefines.NAIL_DESIGN_PATH}{designName}/";
			if (!Directory.Exists(designPath)) return false;

			if (!File.Exists($"{designPath}_design.json")) return false;
			return true;
		}
		
		
		#region Material Cash

		private static Dictionary<string, Material> _createdMaterialCash = new();
		private static Dictionary<string, Material> _previewMaterialCash = new();

		protected static void RegisterCreatedMaterialCash(string key, Material material) {
			_createdMaterialCash.Add(key, material);
		}

		protected static void RegisterPreviewMaterialCash(string key, Material material) {
			_previewMaterialCash.Add(key, material);
		}

		protected static Material? GetCreatedMaterialCash(string key) {
			return _createdMaterialCash.GetValueOrDefault(key);
		}

		protected static Material? GetPreviewMaterialCash(string key) {
			return _previewMaterialCash.GetValueOrDefault(key);
		}

		public static void ClearCreatedMaterialCash() {
			_createdMaterialCash = new Dictionary<string, Material>();
		}

		public static void ClearPreviewMaterialCash() {
			foreach (string key in _previewMaterialCash.Keys) {
				Object.DestroyImmediate(_previewMaterialCash[key]);
			}

			_previewMaterialCash = new Dictionary<string, Material>();
		}


		#endregion
	}
}
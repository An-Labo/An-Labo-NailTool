#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.JsonData;
using world.anlabo.mdnailtool.Editor.Model;

namespace world.anlabo.mdnailtool.Editor.NailDesigns {
	public class JsonMaterialProcessor : NailProcessorBase {
		private static readonly int MainTex = Shader.PropertyToID("_MainTex");

		private readonly NailDesign _nailDesign;

		public JsonMaterialProcessor(string designName, NailDesign nailDesign, DesignData? designData = null)
			: base(designName, designData) {
			this._nailDesign = nailDesign;
		}

		protected override Material GetBaseMaterial(string materialName, string nailShapeName) {
			NailMaterialDelta? delta = FindDelta(materialName, nailShapeName);
			if (delta == null)
				throw new NailToolResourceException("NailDesign", $"materialData not found: {this.DesignName}/{materialName}/{nailShapeName}");

			Shader? shader = Shader.Find(delta.ShaderName);
			if (shader == null)
				throw new NailToolResourceException("NailDesign", $"Shader not found: {delta.ShaderName}");

			var mat = new Material(shader);
			ApplyDelta(mat, delta);
			return mat;
		}

		protected override void ProcessMaterial(Material targetMaterial, string materialName, string colorName, string nailShapeName) {
			string? texGuid = FindMainTexGuid(materialName, nailShapeName, colorName);
			if (string.IsNullOrEmpty(texGuid)) return;

			string texPath = AssetDatabase.GUIDToAssetPath(texGuid!);
			Texture2D? tex = MDNailToolAssetLoader.LoadAssetSafe<Texture2D>(texPath);
			if (tex != null) targetMaterial.SetTexture(MainTex, tex);
		}

		public override bool IsInstalledMaterialVariation(string materialName) {
			if (this._nailDesign.MaterialData == null) return false;
			return this._nailDesign.MaterialData.Keys.Any(k => MatchesMaterialName(k, materialName));
		}

		public override bool IsInstalledColorVariation(string materialName, string colorName) {
			if (this._nailDesign.ColorTextures == null) return false;
			foreach (var shapeEntry in this._nailDesign.ColorTextures.Values) {
				foreach (var matEntry in shapeEntry) {
					if (!MatchesMaterialName(matEntry.Key, materialName)) continue;
					if (matEntry.Value.ContainsKey(colorName)) return true;
				}
			}
			return false;
		}

		public override bool IsSupportedNailShape(string shapeName) {
			if (this._nailDesign.MaterialData == null) return false;
			return this._nailDesign.MaterialData.Keys.Any(k => ShapeEquals(ExtractShape(k), shapeName));
		}

		// "[mat][X][lil-toon]oval" or "[mat][X][lil-toon][Var]_oval" -> "oval"
		private static string ExtractShape(string matKey) {
			int lastBracket = matKey.LastIndexOf(']');
			if (lastBracket >= 0 && lastBracket < matKey.Length - 1)
				return matKey.Substring(lastBracket + 1).TrimStart('_').Trim();
			return matKey;
		}

		private static bool ShapeEquals(string a, string b) =>
			string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);

		private static bool MatchesMaterialName(string matKey, string materialName) {
			return matKey.IndexOf(materialName, System.StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private NailMaterialDelta? FindDelta(string materialName, string nailShapeName) {
			if (this._nailDesign.MaterialData == null) return null;
			foreach (var kv in this._nailDesign.MaterialData) {
				if (MatchesMaterialName(kv.Key, materialName) && ShapeEquals(ExtractShape(kv.Key), nailShapeName))
					return kv.Value;
			}
			// shape 一致のみでフォールバック
			foreach (var kv in this._nailDesign.MaterialData) {
				if (ShapeEquals(ExtractShape(kv.Key), nailShapeName)) return kv.Value;
			}
			return null;
		}

		private string? FindMainTexGuid(string materialName, string nailShapeName, string colorName) {
			if (this._nailDesign.ColorTextures == null) return null;
			// colorTextures のキーは小文字 ("oval") で格納されているため OrdinalIgnoreCase で検索
			foreach (var shapeKv in this._nailDesign.ColorTextures) {
				if (!ShapeEquals(shapeKv.Key, nailShapeName)) continue;
				foreach (var kv in shapeKv.Value) {
					if (!MatchesMaterialName(kv.Key, materialName)) continue;
					if (kv.Value.TryGetValue(colorName, out string? guid)) return guid;
				}
			}
			return null;
		}

		private static void ApplyDelta(Material mat, NailMaterialDelta delta) {
			if (delta.Textures != null) {
				foreach (var kv in delta.Textures) {
					string texPath = AssetDatabase.GUIDToAssetPath(kv.Value);
					Texture? tex = MDNailToolAssetLoader.LoadAssetSafe<Texture>(texPath);
					if (tex != null) mat.SetTexture(kv.Key, tex);
				}
			}

			if (delta.Floats != null) {
				foreach (var kv in delta.Floats) mat.SetFloat(kv.Key, kv.Value);
			}

			if (delta.Colors != null) {
				foreach (var kv in delta.Colors) {
					float[] c = kv.Value;
					if (c.Length >= 4) mat.SetColor(kv.Key, new Color(c[0], c[1], c[2], c[3]));
				}
			}

			if (delta.Vectors != null) {
				foreach (var kv in delta.Vectors) {
					float[] v = kv.Value;
					if (v.Length >= 4) mat.SetVector(kv.Key, new Vector4(v[0], v[1], v[2], v[3]));
				}
			}
		}
	}
}

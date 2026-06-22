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

		// 全 lilToon material で 100% 一致するプロパティ. nailDesign.json から omit, ここで補完.
		private static readonly Dictionary<string, float> GlobalFloatDefaults = new() {
			{ "_lilToonVersion", 45f },
			{ "_Cull", 0f },
			{ "_VertexLightStrength", 1f },
			{ "_LightMinLimit", 0f },
			{ "_UseBumpMap", 1f },
			{ "_UseMatCap", 1f },
			{ "_SpecularToon", 0f },
			{ "_UseReflection", 1f },
		};
		private static readonly Dictionary<string, float[]> GlobalVectorDefaults = new() {
			{ "_LightDirectionOverride", new[] { 0f, 0.001f, 0f, 0f } },
		};
		private const string GlobalShaderDefault = "lilToon";

		protected override Material GetBaseMaterial(string materialName, string nailShapeName) {
			NailMaterialDelta? delta = FindDelta(materialName, nailShapeName);
			if (delta == null)
				throw new NailToolResourceException("NailDesign", $"materialData not found: {this.DesignName}/{materialName}/{nailShapeName}");

			// ShaderName 未指定なら default (lilToon).
			string shaderName = string.IsNullOrEmpty(delta.ShaderName) ? GlobalShaderDefault : delta.ShaderName;
			Shader? shader = Shader.Find(shaderName);
			if (shader == null)
				throw new NailToolResourceException("NailDesign", $"Shader not found: {shaderName}");

			var mat = new Material(shader);
			ApplyDefaults(mat, this._nailDesign.MatCapDefault);
			ApplyDelta(mat, delta);
			return mat;
		}

		// global + per-design default を delta 適用前に当てる. delta の個別値が後で上書き.
		private static void ApplyDefaults(Material mat, string? matCapDefaultGuid) {
			foreach (var kv in GlobalFloatDefaults) {
				if (mat.HasProperty(kv.Key)) mat.SetFloat(kv.Key, kv.Value);
			}
			foreach (var kv in GlobalVectorDefaults) {
				if (mat.HasProperty(kv.Key)) {
					float[] v = kv.Value;
					mat.SetVector(kv.Key, new Vector4(v[0], v[1], v[2], v[3]));
				}
			}
			if (!string.IsNullOrEmpty(matCapDefaultGuid) && mat.HasProperty("_MatCapTex")) {
				string path = AssetDatabase.GUIDToAssetPath(matCapDefaultGuid);
				Texture? tex = MDNailToolAssetLoader.LoadAssetSafe<Texture>(path);
				if (tex != null) mat.SetTexture("_MatCapTex", tex);
			}
		}

		protected override void ProcessMaterial(Material targetMaterial, string materialName, string colorName, string nailShapeName) {
			Texture2D? tex = null;
			string? texGuid = FindMainTexGuid(materialName, nailShapeName, colorName);
			if (!string.IsNullOrEmpty(texGuid)) {
				string texPath = AssetDatabase.GUIDToAssetPath(texGuid!);
				tex = MDNailToolAssetLoader.LoadAssetSafe<Texture2D>(texPath);
			}

			// フォールバック: ColorTextures 未登録 (DailyNail 等) なら disk のファイル名規約で探す.
			// パターン: 【{Design}】/[Data]/[Texture]/[{Shape}]/{material}/[tex][{Design}][{shape小}]{material}.png
			if (tex == null) {
				string shapeLower = nailShapeName.ToLowerInvariant();
				string fallbackPath = $"{MDNailToolDefines.LEGACY_DESIGN_PATH}【{this.DesignName}】/[Data]/[Texture]/[{nailShapeName}]/{materialName}/[tex][{this.DesignName}][{shapeLower}]{materialName}.png";
				tex = MDNailToolAssetLoader.LoadAssetSafe<Texture2D>(fallbackPath);
			}

			if (tex != null) targetMaterial.SetTexture(MainTex, tex);
		}

		public override bool IsInstalledMaterialVariation(string materialName) {
			if (this._nailDesign.MaterialData == null) return false;
			if (!this._nailDesign.MaterialData.Keys.Any(k => MatchesMaterialName(k, materialName))) return false;

			// DB に登録あっても disk 上に .mat 未展開 (extract 前) なら hidden. DailyNail #001-#260 等の ghost エントリ抑止.
			string dataDir = $"{MDNailToolDefines.LEGACY_DESIGN_PATH}【{this.DesignName}】/[Data]";
			if (!System.IO.Directory.Exists(dataDir)) return false;
			string pattern = string.IsNullOrEmpty(materialName) ? "*.mat" : $"*{materialName}*.mat";
			return System.IO.Directory.EnumerateFiles(dataDir, pattern, System.IO.SearchOption.AllDirectories).Any();
		}

		public override bool IsInstalledColorVariation(string materialName, string colorName) {
			string normalizedColor = colorName.Trim('[', ']');
			if (this._nailDesign.ColorTextures != null) {
				foreach (var shapeEntry in this._nailDesign.ColorTextures.Values) {
					foreach (var matEntry in shapeEntry) {
						if (!MatchesMaterialName(matEntry.Key, materialName)) continue;
						if (matEntry.Value.Keys.Any(k => string.Equals(k, normalizedColor, System.StringComparison.OrdinalIgnoreCase))) return true;
					}
				}
			}

			// フォールバック: ColorTextures 未登録 (DailyNail 等) なら disk 上の texture 存在で判定.
			string textureDir = $"{MDNailToolDefines.LEGACY_DESIGN_PATH}【{this.DesignName}】/[Data]/[Texture]";
			if (!System.IO.Directory.Exists(textureDir)) return false;
			string pattern = string.IsNullOrEmpty(materialName) ? "*.png" : $"*{materialName}*.png";
			return System.IO.Directory.EnumerateFiles(textureDir, pattern, System.IO.SearchOption.AllDirectories).Any();
		}

		public override bool IsSupportedNailShape(string shapeName) {
			if (this._nailDesign.MaterialData == null) return false;
			return this._nailDesign.MaterialData.Keys.Any(k => ShapeEquals(ExtractShape(k), shapeName));
		}

		// "[mat][X][lil-toon]oval" or "[mat][X][lil-toon]Var_oval" -> "oval"
		private static string ExtractShape(string matKey) {
			int lastBracket = matKey.LastIndexOf(']');
			string suffix = lastBracket >= 0 && lastBracket < matKey.Length - 1
				? matKey.Substring(lastBracket + 1).TrimStart('_').Trim()
				: matKey;
			int lastUnderscore = suffix.LastIndexOf('_');
			if (lastUnderscore >= 0 && lastUnderscore < suffix.Length - 1)
				return suffix.Substring(lastUnderscore + 1).Trim();
			return string.IsNullOrEmpty(suffix) ? matKey : suffix;
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
			string normalizedColor = colorName.Trim('[', ']');
			foreach (var shapeKv in this._nailDesign.ColorTextures) {
				if (!ShapeEquals(shapeKv.Key, nailShapeName)) continue;
				foreach (var kv in shapeKv.Value) {
					if (!MatchesMaterialName(kv.Key, materialName)) continue;
					string? matchKey = kv.Value.Keys.FirstOrDefault(k => string.Equals(k, normalizedColor, System.StringComparison.OrdinalIgnoreCase));
					if (matchKey != null) return kv.Value[matchKey];
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

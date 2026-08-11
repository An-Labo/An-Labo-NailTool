#nullable enable

using System;
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

		private readonly Dictionary<string, string?> _compatibleTexturePathCache = new(StringComparer.Ordinal);

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
			ApplyDefaults(mat, this._nailDesign.MatCapDefault, this.DesignName);
			ApplyDelta(mat, delta, this.DesignName, materialName, nailShapeName);
			return mat;
		}

		// global + per-design default を delta 適用前に当てる. delta の個別値が後で上書き.
		private static void ApplyDefaults(Material mat, string? matCapDefaultGuid, string designName) {
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
				Texture? tex = LoadTextureWithFallback(matCapDefaultGuid, "_MatCapTex", designName);
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

			// フォールバック: ColorTextures GUID 解決失敗 (.meta 再生成等) or 未登録なら disk のファイル名規約で探す.
			// 試行: [1] color 軸型 / [2] material 軸型 / [3] color + material 複合型
			if (tex == null) {
				string shapeLower = nailShapeName.ToLowerInvariant();
				string normalizedColor = colorName.Trim('[', ']');
				string designRoot = $"{MDNailToolDefines.LEGACY_DESIGN_PATH}【{this.DesignName}】/[Data]/[Texture]/[{nailShapeName}]";
				string fileNamePrefix = $"[tex][{this.DesignName}][{shapeLower}]";
				string materialDirectory = $"{designRoot}/{materialName}";
				// 複合型の実ファイル名は colorName + materialName の単純連結。
				// 角括弧は構文ではなく各名称の一部 (例: Horo は色名側、SimpleNailSet は素材名側)。
				string compositeStem = $"{fileNamePrefix}{colorName}{materialName}";
				string[] candidates = {
					$"{designRoot}/{fileNamePrefix}{normalizedColor}.png",
					$"{materialDirectory}/{fileNamePrefix}{materialName}.png",
					$"{materialDirectory}/{compositeStem}.png",
				};
				foreach (string path in candidates) {
					tex = MDNailToolAssetLoader.LoadAssetSafe<Texture2D>(path);
					if (tex != null) break;
				}

				// 完全一致しない旧資産は、表記だけを正規化して一意に一致する場合に限り採用する。
				// 全半角・大小文字・空白と、末尾のローカライズ補助名の差だけを許容する。
				if (tex == null && System.IO.Directory.Exists(materialDirectory)) {
					string expectedVariantKey = NormalizeTextureVariantKey($"{colorName}{materialName}");
					string? assetPath = FindCompatibleTexturePath(materialDirectory, fileNamePrefix, expectedVariantKey);
					if (!string.IsNullOrEmpty(assetPath)) {
						tex = MDNailToolAssetLoader.LoadAssetSafe<Texture2D>(assetPath);
					}
				}
			}

			if (tex != null) targetMaterial.SetTexture(MainTex, tex);
		}

		private string? FindCompatibleTexturePath(string materialDirectory, string fileNamePrefix, string expectedVariantKey) {
			try {
				// import後にdirectory timestampが変われば、negative cacheを含め自動的に再評価する。
				long directoryVersion = System.IO.Directory.GetLastWriteTimeUtc(materialDirectory).Ticks;
				string cacheKey = $"{materialDirectory}\n{directoryVersion}\n{fileNamePrefix}\n{expectedVariantKey}";
				if (this._compatibleTexturePathCache.TryGetValue(cacheKey, out string? cachedPath)) return cachedPath;

				string? result = null;
				string[] compatiblePaths = System.IO.Directory
					.EnumerateFiles(materialDirectory, "*", System.IO.SearchOption.TopDirectoryOnly)
					.Where(path => string.Equals(System.IO.Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
					.Where(path => IsCompatibleTextureFileName(
						System.IO.Path.GetFileNameWithoutExtension(path),
						fileNamePrefix,
						expectedVariantKey))
					.Take(2)
					.ToArray();
				if (compatiblePaths.Length == 1) result = compatiblePaths[0].Replace('\\', '/');
				this._compatibleTexturePathCache[cacheKey] = result;
				return result;
			}
			catch (System.IO.IOException) {
				// Asset import中などの一時エラーはcacheせず、次回再試行する。
			}
			catch (UnauthorizedAccessException) {
				// 読み取れない場所から曖昧な代替を選ばない。
			}

			return null;
		}

		internal static bool IsCompatibleTextureFileName(string actualStem, string requiredPrefix, string expectedVariantKey) {
			if (!actualStem.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase)) return false;
			string actualVariant = actualStem.Substring(requiredPrefix.Length);
			return string.Equals(
				NormalizeTextureVariantKey(actualVariant),
				expectedVariantKey,
				StringComparison.Ordinal);
		}

		internal static string NormalizeTextureVariantKey(string value) {
			string normalized = value.Normalize(NormalizationForm.FormKC).Trim();
			normalized = RemoveTrailingLocalizedLabel(normalized);
			var builder = new StringBuilder(normalized.Length);
			foreach (char character in normalized) {
				if (char.IsWhiteSpace(character)) continue;
				builder.Append(char.ToLowerInvariant(character));
			}
			return builder.ToString();
		}

		private static string RemoveTrailingLocalizedLabel(string value) {
			if (value.Length < 3) return value;
			char closing = value[value.Length - 1];
			char opening = closing == ')' ? '(' : closing == '）' ? '（' : '\0';
			if (opening == '\0') return value;
			int openingIndex = value.LastIndexOf(opening);
			if (openingIndex <= 0 || openingIndex >= value.Length - 2) return value;

			string label = value.Substring(openingIndex + 1, value.Length - openingIndex - 2);
			bool hasLocalizedLetterOrDigit = label.Any(character =>
				character > '\u007f' && char.IsLetterOrDigit(character));
			return hasLocalizedLetterOrDigit
				? value.Substring(0, openingIndex).TrimEnd()
				: value;
		}

		public override bool IsInstalledMaterialVariation(string materialName) {
			if (this._nailDesign.MaterialData == null) return false;
			if (!this._nailDesign.MaterialData.Keys.Any(k => MatchesMaterialName(k, materialName))) return false;
			return HasTexturePngContaining(materialName);
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
			return HasTexturePngContaining(materialName);
		}

		private List<string>? _texturePngNames;

		// 初回のみ textureDir 全 png を 1 回列挙して cache. materialData ×300 呼びのハング (数十秒) 対策.
		private bool HasTexturePngContaining(string materialName) {
			if (this._texturePngNames == null) {
				string textureDir = $"{MDNailToolDefines.LEGACY_DESIGN_PATH}【{this.DesignName}】/[Data]/[Texture]";
				if (!System.IO.Directory.Exists(textureDir)) {
					this._texturePngNames = new List<string>();
				} else {
					this._texturePngNames = System.IO.Directory
						.EnumerateFiles(textureDir, "*.png", System.IO.SearchOption.AllDirectories)
						.Select(System.IO.Path.GetFileName)
						.ToList();
				}
			}
			if (string.IsNullOrEmpty(materialName)) return this._texturePngNames.Count > 0;
			return this._texturePngNames.Any(n => n.IndexOf(materialName, System.StringComparison.OrdinalIgnoreCase) >= 0);
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
			string normalizedMaterial = materialName.Trim('[', ']');
			foreach (var shapeKv in this._nailDesign.ColorTextures) {
				if (!ShapeEquals(shapeKv.Key, nailShapeName)) continue;
				foreach (var kv in shapeKv.Value) {
					// 材料軸型: matKey に materialName を含む + colorKey が color 単体
					if (MatchesMaterialName(kv.Key, materialName)) {
						string? matchKey = kv.Value.Keys.FirstOrDefault(k => string.Equals(k, normalizedColor, System.StringComparison.OrdinalIgnoreCase));
						if (matchKey != null) return kv.Value[matchKey];
					}
					// 複合キー型 (HoroNail/SimpleNailSet 等): matKey は shape 汎用、colorKey が color と material の複合
					foreach (var colorKv in kv.Value) {
						if (MatchesCompositeColorKey(colorKv.Key, normalizedColor, normalizedMaterial))
							return colorKv.Value;
					}
				}
			}
			return null;
		}

		// colorKey が `[color]material` (HoroNail 型) or `color[material]` (SimpleNailSet 型) の複合形式で
		// 指定 color/material の両方を含むか判定. bracket 位置に依存しない.
		private static bool MatchesCompositeColorKey(string colorKey, string normalizedColor, string normalizedMaterial) {
			if (string.IsNullOrEmpty(normalizedColor) || string.IsNullOrEmpty(normalizedMaterial)) return false;
			return colorKey.IndexOf(normalizedColor, System.StringComparison.OrdinalIgnoreCase) >= 0
				&& colorKey.IndexOf(normalizedMaterial, System.StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static Texture? LoadTextureWithFallback(string? guid, string propertyName, string designName, string? materialName = null, string? nailShapeName = null) {
			if (string.IsNullOrEmpty(guid)) return null;

			string path = AssetDatabase.GUIDToAssetPath(guid!);
			Texture? tex = MDNailToolAssetLoader.LoadAssetSafe<Texture>(path);
			if (tex != null) return tex;

			if (!string.Equals(propertyName, "_MatCapTex", StringComparison.Ordinal)) return null;

			tex = LoadKnownMatCapFallback(guid!);
			if (tex != null) return tex;

			return null;
		}

		private static Texture? LoadKnownMatCapFallback(string guid) {
			string? fallbackPath = guid switch {
				"81aa5acab4400e54486f930dda82be43" => MDNailToolDefines.LEGACY_DESIGN_PATH + "[CommonData]/[matcap][Nail].png",
				"1856b75e28b11f740bf4b6b0201c1f9a" => MDNailToolDefines.LEGACY_DESIGN_PATH + "[CommonData]/[matcap][MatNail].png",
				"9673a195c8412fa40970e1ae03d7b7dd" => MDNailToolDefines.LEGACY_DESIGN_PATH + "[CommonData]/[matcap][BlightNail].png",
				_ => null,
			};
			return string.IsNullOrEmpty(fallbackPath) ? null : MDNailToolAssetLoader.LoadAssetSafe<Texture>(fallbackPath);
		}

		private static void ApplyDelta(Material mat, NailMaterialDelta delta, string designName, string materialName, string nailShapeName) {
			if (delta.Textures != null) {
				foreach (var kv in delta.Textures) {
					if (kv.Key == "_BaseMap" || kv.Key == "_BaseColorMap") continue;
					Texture? tex = LoadTextureWithFallback(kv.Value, kv.Key, designName, materialName, nailShapeName);
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

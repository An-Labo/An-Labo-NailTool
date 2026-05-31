#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;

namespace world.anlabo.mdnailtool.Editor.Develop {
	internal static class NailResourceJsonConverter {
		private const string MENU = "An-Labo/Develop/Convert Nail Resources to JSON";
		private const string MAIN_TEX_KEY = "_MainTex";

		[MenuItem(MENU)]
		private static void Run() {
			try {
				ConvertMaterials();
				ConvertPrefabs();
				Debug.Log("[NailResourceJsonConverter] 変換完了");
			} catch (Exception ex) {
				Debug.LogError($"[NailResourceJsonConverter] 変換失敗: {ex}");
			}
		}

		private static void ConvertMaterials() {
			string designRoot = MDNailToolDefines.NAIL_DESIGN_PATH.TrimEnd('/');
			if (!AssetDatabase.IsValidFolder(designRoot)) {
				Debug.LogWarning($"[NailConverter] Design フォルダが見つかりません: {designRoot}");
				return;
			}

			string dbAssetPath = MDNailToolDefines.DB_PATH + "nailDesign.json";
			string? dbFullPath = AssetPathToFullPath(dbAssetPath);
			if (dbFullPath == null) { Debug.LogError("[NailConverter] nailDesign.json が見つかりません"); return; }

			JObject db = JObject.Parse(File.ReadAllText(dbFullPath));
			string[] designDirs = AssetDatabase.GetSubFolders(designRoot);
			int converted = 0;

			foreach (string designDir in designDirs) {
				string designName = Path.GetFileName(designDir);
				JToken? entry = db[designName];
				if (entry == null) continue;

				string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { designDir });
				if (matGuids.Length == 0) continue;

				var materialData = new Dictionary<string, NailMaterialDelta>();
				// shape -> matName -> colorKey -> _MainTex GUID
				var colorTextures = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

				foreach (string guid in matGuids) {
					string matPath = AssetDatabase.GUIDToAssetPath(guid);
					Material? mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
					if (mat == null) continue;

					string matFileName = Path.GetFileNameWithoutExtension(matPath);
					string shape = ExtractShape(matFileName);

					materialData[matFileName] = BuildDelta(mat, matFileName);

					ScanLegacyColorTextures(designName, shape, matFileName, colorTextures);
				}

				entry["materialData"] = JToken.FromObject(materialData, JsonSerializer.CreateDefault());
				entry["colorTextures"] = JToken.FromObject(colorTextures, JsonSerializer.CreateDefault());
				converted++;
			}

			File.WriteAllText(dbFullPath, db.ToString(Formatting.Indented));
			AssetDatabase.ImportAsset(dbAssetPath, ImportAssetOptions.ForceUpdate);
			DBNailDesign.ClearCache();
			Debug.Log($"[NailConverter] material 変換: {converted} デザイン");
		}

		private static void ConvertPrefabs() {
			string dbAssetPath = MDNailToolDefines.DB_PATH + "shop.json";
			string? dbFullPath = AssetPathToFullPath(dbAssetPath);
			if (dbFullPath == null) { Debug.LogError("[NailConverter] shop.json が見つかりません"); return; }

			JObject db = JObject.Parse(File.ReadAllText(dbFullPath));
			var prefabCache = new Dictionary<string, NailPrefabNodeData[]>();

			foreach (var shopProp in db.Properties()) {
				JToken? avatarsToken = shopProp.Value["avatars"];
				if (avatarsToken == null) continue;

				foreach (var avatarProp in ((JObject)avatarsToken).Properties()) {
					JToken? variationsToken = avatarProp.Value["avatarVariations"];
					if (variationsToken == null) continue;

					foreach (var varProp in ((JObject)variationsToken).Properties()) {
						JToken varToken = varProp.Value;
						string? guid = varToken["nailPrefabGUID"]?.ToString();
						if (string.IsNullOrEmpty(guid)) continue;

						if (!prefabCache.TryGetValue(guid!, out NailPrefabNodeData[]? nodes)) {
							nodes = BuildPrefabNodes(guid!);
							prefabCache[guid!] = nodes ?? Array.Empty<NailPrefabNodeData>();
						}

						if (nodes != null && nodes.Length > 0)
							varToken["nailNodes"] = JToken.FromObject(nodes, JsonSerializer.CreateDefault());
					}
				}
			}

			File.WriteAllText(dbFullPath, db.ToString(Formatting.Indented));
			AssetDatabase.ImportAsset(dbAssetPath, ImportAssetOptions.ForceUpdate);
			Debug.Log($"[NailConverter] prefab 変換: {prefabCache.Count} GUID");
		}

		private static NailMaterialDelta BuildDelta(Material mat, string matName) {
			var delta = new NailMaterialDelta {
				ShaderName = mat.shader?.name ?? "",
				MaterialName = matName,
			};

			Shader shader = mat.shader;
			if (shader == null) return delta;

			var textures = new Dictionary<string, string>();
			var floats = new Dictionary<string, float>();
			var colors = new Dictionary<string, float[]>();
			var vectors = new Dictionary<string, float[]>();

			int propCount = shader.GetPropertyCount();
			for (int i = 0; i < propCount; i++) {
				string propName = shader.GetPropertyName(i);
				switch (shader.GetPropertyType(i)) {
					case UnityEngine.Rendering.ShaderPropertyType.Texture: {
						if (propName == MAIN_TEX_KEY) break;
						Texture? tex = mat.GetTexture(propName);
						if (tex == null) break;
						string texGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tex));
						if (!string.IsNullOrEmpty(texGuid)) textures[propName] = texGuid;
						break;
					}
					case UnityEngine.Rendering.ShaderPropertyType.Float:
					case UnityEngine.Rendering.ShaderPropertyType.Range: {
						float val = mat.GetFloat(propName);
						if (val != 0f) floats[propName] = val;
						break;
					}
					case UnityEngine.Rendering.ShaderPropertyType.Color: {
						Color c = mat.GetColor(propName);
						if (c != Color.black) colors[propName] = new[] { c.r, c.g, c.b, c.a };
						break;
					}
					case UnityEngine.Rendering.ShaderPropertyType.Vector: {
						Vector4 v = mat.GetVector(propName);
						if (v != Vector4.zero) vectors[propName] = new[] { v.x, v.y, v.z, v.w };
						break;
					}
				}
			}

			if (textures.Count > 0) delta.Textures = textures;
			if (floats.Count > 0) delta.Floats = floats;
			if (colors.Count > 0) delta.Colors = colors;
			if (vectors.Count > 0) delta.Vectors = vectors;
			return delta;
		}

		private static NailPrefabNodeData[]? BuildPrefabNodes(string prefabGuid) {
			string path = AssetDatabase.GUIDToAssetPath(prefabGuid);
			if (string.IsNullOrEmpty(path)) return null;
			GameObject? root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (root == null) return null;
			return new[] { BuildNode(root.transform) };
		}

		private static NailPrefabNodeData BuildNode(Transform t) {
			var node = new NailPrefabNodeData {
				Name = t.gameObject.name,
				LocalPosition = new[] { t.localPosition.x, t.localPosition.y, t.localPosition.z },
				LocalRotation = new[] { t.localRotation.x, t.localRotation.y, t.localRotation.z, t.localRotation.w },
				LocalScale = new[] { t.localScale.x, t.localScale.y, t.localScale.z },
			};

			if (t.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer smr) && smr.sharedMesh != null) {
				node.MeshGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(smr.sharedMesh));
				node.RootBoneName = smr.rootBone?.name;

				if (smr.sharedMesh.blendShapeCount > 0) {
					var weights = new Dictionary<string, float>();
					for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++) {
						float w = smr.GetBlendShapeWeight(i);
						if (w != 0f) weights[smr.sharedMesh.GetBlendShapeName(i)] = w;
					}
					if (weights.Count > 0) node.BlendShapeWeights = weights;
				}
			}

			if (t.childCount > 0) {
				var children = new NailPrefabNodeData[t.childCount];
				for (int i = 0; i < t.childCount; i++)
					children[i] = BuildNode(t.GetChild(i));
				node.Children = children;
			}

			return node;
		}

		private static string GetTextureGuid(Material mat, string propName) {
			Texture? tex = mat.GetTexture(propName);
			if (tex == null) return "";
			return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tex));
		}

		// "]" 以降を取り、バリアント名_shape 形式なら最後の _ 以降を shape とする
		private static string ExtractShape(string matFileName) {
			int lastBracket = matFileName.LastIndexOf(']');
			string suffix = lastBracket >= 0 && lastBracket < matFileName.Length - 1
				? matFileName.Substring(lastBracket + 1).TrimStart('_').Trim()
				: matFileName;
			int lastUnderscore = suffix.LastIndexOf('_');
			if (lastUnderscore >= 0 && lastUnderscore < suffix.Length - 1)
				return suffix.Substring(lastUnderscore + 1).Trim();
			return string.IsNullOrEmpty(suffix) ? "default" : suffix;
		}

		private static void ScanLegacyColorTextures(
			string designName, string shape, string matFileName,
			Dictionary<string, Dictionary<string, Dictionary<string, string>>> colorTextures) {

			string texRootAsset = $"{MDNailToolDefines.LEGACY_DESIGN_PATH}【{designName}】/[Data]/[Texture]";
			string? texRootFull = AssetFolderToFullPath(texRootAsset);
			if (texRootFull == null) return;

			// Find shape subdir case-insensitively (e.g., "[Oval]")
			string shapeDirFull = "";
			foreach (string sub in Directory.GetDirectories(texRootFull)) {
				string leaf = Path.GetFileName(sub).Trim('[', ']');
				if (string.Equals(leaf, shape, StringComparison.OrdinalIgnoreCase)) {
					shapeDirFull = sub;
					break;
				}
			}
			if (string.IsNullOrEmpty(shapeDirFull)) return;

			string prefix = $"[tex][{designName}][{shape}]";

			// Textures directly in shape dir (no material variant)
			CollectColorTextures(shapeDirFull, prefix, "", matFileName, shape, colorTextures);

			// Textures in material-named subdirs
			foreach (string matSubFull in Directory.GetDirectories(shapeDirFull)) {
				string matVariant = Path.GetFileName(matSubFull);
				if (!matFileName.Contains(matVariant, StringComparison.OrdinalIgnoreCase) &&
					!matFileName.Contains(matVariant.Trim('[', ']'), StringComparison.OrdinalIgnoreCase)) continue;
				CollectColorTextures(matSubFull, prefix, matVariant, matFileName, shape, colorTextures);
			}
		}

		private static void CollectColorTextures(
			string dirFull, string prefix, string matVariantName,
			string matFileName, string shape,
			Dictionary<string, Dictionary<string, Dictionary<string, string>>> colorTextures) {

			foreach (string texFull in Directory.GetFiles(dirFull, "*.png", SearchOption.TopDirectoryOnly)) {
				string texName = Path.GetFileNameWithoutExtension(texFull);
				if (!texName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
				string remainder = texName.Substring(prefix.Length);

				if (!string.IsNullOrEmpty(matVariantName) &&
					remainder.EndsWith(matVariantName, StringComparison.OrdinalIgnoreCase))
					remainder = remainder.Substring(0, remainder.Length - matVariantName.Length);

				remainder = remainder.Trim('[', ']');
				if (string.IsNullOrEmpty(remainder)) continue;

				string texAssetPath = FullPathToAssetPath(texFull);
				if (string.IsNullOrEmpty(texAssetPath)) continue;
				string texGuid = AssetDatabase.AssetPathToGUID(texAssetPath);
				if (string.IsNullOrEmpty(texGuid)) continue;

				if (!colorTextures.ContainsKey(shape))
					colorTextures[shape] = new Dictionary<string, Dictionary<string, string>>();
				if (!colorTextures[shape].ContainsKey(matFileName))
					colorTextures[shape][matFileName] = new Dictionary<string, string>();
				if (!colorTextures[shape][matFileName].ContainsKey(remainder))
					colorTextures[shape][matFileName][remainder] = texGuid;
			}
		}

		private static string? AssetFolderToFullPath(string assetPath) {
			string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
			return Directory.Exists(full) ? full : null;
		}

		private static string FullPathToAssetPath(string fullPath) {
			string dataPath = Application.dataPath.Replace('\\', '/');
			string normalized = fullPath.Replace('\\', '/');
			int idx = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
			if (idx >= 0) return normalized.Substring(idx + 1);
			// fallback: relative to project root
			string projectRoot = dataPath.Substring(0, dataPath.Length - "/Assets".Length);
			if (normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
				return normalized.Substring(projectRoot.Length).TrimStart('/');
			return "";
		}

		private static string? AssetPathToFullPath(string assetPath) {
			// "Assets/..." -> absolute path
			if (!assetPath.StartsWith("Assets/") && !assetPath.StartsWith("Packages/"))
				return null;
			string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
			return File.Exists(fullPath) ? fullPath : null;
		}
	}
}

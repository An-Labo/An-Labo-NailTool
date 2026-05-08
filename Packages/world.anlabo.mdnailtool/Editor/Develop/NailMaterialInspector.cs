#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace world.anlabo.mdnailtool.Editor.Develop {
	internal static class NailMaterialInspector {
		// lilToon: feature flag prefix -> 影響範囲. flag が 0 ならこの prefix を持つプロパティは無視
		private static readonly Dictionary<string, string[]> FeatureFlagPrefix = new() {
			{ "_UseShadow",          new[] { "_Shadow", "_1st_Shade", "_2nd_Shade", "_ShadowBorder", "_ShadowBlur", "_ShadowReceive", "_ShadowEnvStrength", "_ShadowMainStrength", "_ShadowStrength", "_ShadowMaskType", "_ShadowAOShift", "_ShadowPostAO" } },
			{ "_UseEmission",        new[] { "_Emission", "_EmissionMap", "_EmissionBlend", "_EmissionGrad", "_EmissionFluorescence", "_EmissionMainStrength", "_EmissionParallax" } },
			{ "_UseEmission2nd",     new[] { "_Emission2nd" } },
			{ "_UseBumpMap",         new[] { "_BumpMap", "_BumpScale" } },
			{ "_UseBump2ndMap",      new[] { "_Bump2nd" } },
			{ "_UseAnisotropy",      new[] { "_Anisotropy" } },
			{ "_UseReflection",      new[] { "_Reflection", "_Smoothness", "_Metallic", "_GSAA" } },
			{ "_UseMatCap",          new[] { "_MatCap" } },
			{ "_UseMatCap2nd",       new[] { "_MatCap2nd" } },
			{ "_UseRim",             new[] { "_Rim" } },
			{ "_UseGlitter",         new[] { "_Glitter" } },
			{ "_UseBacklight",       new[] { "_Backlight" } },
			{ "_UseParallax",        new[] { "_Parallax" } },
			{ "_UseAudioLink",       new[] { "_AudioLink" } },
			{ "_UseDissolve",        new[] { "_Dissolve" } },
			{ "_UseOutline",         new[] { "_Outline" } },
			{ "_UseFur",             new[] { "_Fur" } },
			{ "_UseClippingCanceller", new[] { "_ClippingCanceller" } },
			{ "_UseIDMask",          new[] { "_IDMask" } },
			{ "_UseUDIMDiscard",     new[] { "_UDIMDiscard" } },
			{ "_UseGem",             new[] { "_Gem" } },
		};

		private const string MENU_PATH = "An-Labo/Develop/Inspect Nail Materials";

		[MenuItem(MENU_PATH)]
		private static void Run() {
			// NailTool 単体リポジトリでは Packages 側がマスター. AllAvatar 等では Extract 済みの Assets 側のみ存在
			string designRoot = MDNailToolDefines.ROOT_PACKAGE_PATH + "Resource/Nail/Design";
			if (!AssetDatabase.IsValidFolder(designRoot)) {
				designRoot = MDNailToolDefines.NAIL_DESIGN_PATH.TrimEnd('/');
				if (!AssetDatabase.IsValidFolder(designRoot)) {
					Debug.LogWarning($"[NailMaterialInspector] Design folder not found in either Packages or Assets.");
					return;
				}
				Debug.Log($"[NailMaterialInspector] Scanning Assets-side (Extract copy): {designRoot}");
			} else {
				Debug.Log($"[NailMaterialInspector] Scanning Packages-side master: {designRoot}");
			}

			string[] designDirs = AssetDatabase.GetSubFolders(designRoot);
			if (designDirs.Length == 0) {
				Debug.LogWarning($"[NailMaterialInspector] No design folders found under: {designRoot}");
				return;
			}

			StringBuilder customizedReport = new();
			StringBuilder mismatchReport = new();
			int designCount = 0;
			int matCount = 0;
			int mismatchCount = 0;

			Dictionary<string, int> propertyHits = new();

			foreach (string designDir in designDirs) {
				string designName = Path.GetFileName(designDir);
				string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { designDir });
				if (matGuids.Length == 0) continue;

				List<string> matLines = new();
				List<string> mismatchLines = new();

				foreach (string guid in matGuids) {
					string matPath = AssetDatabase.GUIDToAssetPath(guid);
					Material? mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
					if (mat == null || mat.shader == null) continue;
					matCount++;

					HashSet<string> ignoredPrefixes = CollectIgnoredPrefixes(mat);
					List<string> customized = CollectCustomizedProperties(mat, ignoredPrefixes);
					List<string> mismatches = CollectTextureMismatches(mat, designName, ignoredPrefixes);

					if (customized.Count > 0) {
						matLines.Add($"  - `{Path.GetFileName(matPath)}`");
						foreach (string line in customized) {
							matLines.Add($"    - {line}");
							string propName = line.Split(' ')[0];
							propertyHits[propName] = propertyHits.GetValueOrDefault(propName, 0) + 1;
						}
					}

					if (mismatches.Count > 0) {
						mismatchLines.Add($"  - `{Path.GetFileName(matPath)}`");
						foreach (string line in mismatches) {
							mismatchLines.Add($"    - {line}");
							mismatchCount++;
						}
					}
				}

				if (matLines.Count > 0) {
					customizedReport.AppendLine($"### {designName}");
					foreach (string line in matLines) customizedReport.AppendLine(line);
					customizedReport.AppendLine();
				}

				if (mismatchLines.Count > 0) {
					mismatchReport.AppendLine($"### {designName}");
					foreach (string line in mismatchLines) mismatchReport.AppendLine(line);
					mismatchReport.AppendLine();
				}

				designCount++;
			}

			StringBuilder header = new();
			header.AppendLine("# Nail Material Inspection Report");
			header.AppendLine();
			header.AppendLine($"- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			header.AppendLine($"- Design folders: {designCount}");
			header.AppendLine($"- Materials scanned: {matCount}");
			header.AppendLine($"- Texture mismatches: {mismatchCount}");
			header.AppendLine();
			header.AppendLine("## Property Frequency (top)");
			header.AppendLine();
			foreach (KeyValuePair<string, int> kv in propertyHits.OrderByDescending(kv => kv.Value).Take(40)) {
				header.AppendLine($"- {kv.Key}: {kv.Value}");
			}
			header.AppendLine();
			header.AppendLine("## Texture GUID Mismatches (suspect misconfiguration)");
			header.AppendLine();
			header.AppendLine("テクスチャの asset path がデザイン名を含んでいない = 他デザインのテクスチャが紐付いている可能性。");
			header.AppendLine();
			if (mismatchReport.Length == 0) header.AppendLine("(none)").AppendLine();
			else header.Append(mismatchReport);
			header.AppendLine("## Customized Properties (non-default values)");
			header.AppendLine();
			header.Append(customizedReport);

			string reportDir = MDNailToolDefines.REPORT_PATH;
			if (!Directory.Exists(reportDir)) Directory.CreateDirectory(reportDir);
			string reportPath = $"{reportDir}nail-material-inspect_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.md";
			File.WriteAllText(reportPath, header.ToString(), Encoding.UTF8);
			AssetDatabase.Refresh();

			Debug.Log($"[NailMaterialInspector] Report written: {reportPath}\nDesigns: {designCount}, Materials: {matCount}, Mismatches: {mismatchCount}");
			UnityEngine.Object? asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(reportPath);
			if (asset != null) EditorGUIUtility.PingObject(asset);
		}

		private static HashSet<string> CollectIgnoredPrefixes(Material mat) {
			HashSet<string> ignored = new();
			foreach (KeyValuePair<string, string[]> kv in FeatureFlagPrefix) {
				if (!mat.HasProperty(kv.Key)) continue;
				if (mat.GetFloat(kv.Key) == 0f) {
					foreach (string p in kv.Value) ignored.Add(p);
				}
			}
			return ignored;
		}

		private static bool IsIgnored(string propName, HashSet<string> ignoredPrefixes) {
			foreach (string prefix in ignoredPrefixes) {
				if (propName.StartsWith(prefix, StringComparison.Ordinal)) return true;
			}
			return false;
		}

		private static List<string> CollectCustomizedProperties(Material mat, HashSet<string> ignoredPrefixes) {
			List<string> result = new();
			Shader shader = mat.shader;
			int count = shader.GetPropertyCount();

			for (int i = 0; i < count; i++) {
				string propName = shader.GetPropertyName(i);
				if (IsIgnored(propName, ignoredPrefixes)) continue;

				UnityEngine.Rendering.ShaderPropertyType type = shader.GetPropertyType(i);
				switch (type) {
					case UnityEngine.Rendering.ShaderPropertyType.Float:
					case UnityEngine.Rendering.ShaderPropertyType.Range: {
						float v = mat.GetFloat(propName);
						float def = shader.GetPropertyDefaultFloatValue(i);
						if (!Approximately(v, def)) result.Add($"{propName} = {v.ToString("R", CultureInfo.InvariantCulture)} (default {def.ToString("R", CultureInfo.InvariantCulture)})");
						break;
					}
					case UnityEngine.Rendering.ShaderPropertyType.Color: {
						Color v = mat.GetColor(propName);
						Vector4 d = shader.GetPropertyDefaultVectorValue(i);
						Color def = new(d.x, d.y, d.z, d.w);
						if (!ApproxColor(v, def)) result.Add($"{propName} = {FormatColor(v)} (default {FormatColor(def)})");
						break;
					}
					case UnityEngine.Rendering.ShaderPropertyType.Vector: {
						Vector4 v = mat.GetVector(propName);
						Vector4 def = shader.GetPropertyDefaultVectorValue(i);
						if (!ApproxVector(v, def)) result.Add($"{propName} = {FormatVector(v)} (default {FormatVector(def)})");
						break;
					}
					case UnityEngine.Rendering.ShaderPropertyType.Texture: {
						Texture? t = mat.GetTexture(propName);
						if (t != null) result.Add($"{propName} = {AssetDatabase.GetAssetPath(t)}");
						break;
					}
				}
			}
			return result;
		}

		private static List<string> CollectTextureMismatches(Material mat, string designName, HashSet<string> ignoredPrefixes) {
			List<string> result = new();
			Shader shader = mat.shader;
			int count = shader.GetPropertyCount();
			for (int i = 0; i < count; i++) {
				string propName = shader.GetPropertyName(i);
				if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
				if (IsIgnored(propName, ignoredPrefixes)) continue;
				Texture? tex = mat.GetTexture(propName);
				if (tex == null) continue;
				string path = AssetDatabase.GetAssetPath(tex);
				if (string.IsNullOrEmpty(path)) continue;
				if (path.IndexOf(designName, StringComparison.OrdinalIgnoreCase) >= 0) continue;
				result.Add($"{propName} -> `{path}` (asset path does not contain `{designName}`)");
			}
			return result;
		}

		private static bool Approximately(float a, float b) => Mathf.Abs(a - b) < 1e-5f;
		private static bool ApproxColor(Color a, Color b) => Approximately(a.r, b.r) && Approximately(a.g, b.g) && Approximately(a.b, b.b) && Approximately(a.a, b.a);
		private static bool ApproxVector(Vector4 a, Vector4 b) => Approximately(a.x, b.x) && Approximately(a.y, b.y) && Approximately(a.z, b.z) && Approximately(a.w, b.w);
		private static string FormatColor(Color c) => $"RGBA({c.r:0.###}, {c.g:0.###}, {c.b:0.###}, {c.a:0.###})";
		private static string FormatVector(Vector4 v) => $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###}, {v.w:0.###})";
	}
}

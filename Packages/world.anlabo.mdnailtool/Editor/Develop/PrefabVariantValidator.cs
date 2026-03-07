// #define MD_NAIL_DEVELOP

#if MD_NAIL_DEVELOP
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Develop {
	/// <summary>
	/// Prefabフォルダ内のバリアントプレハブの整合性を検証するスクリプト。
	/// メニュー: An-Labo/Develop/Validate Variant Prefabs
	/// </summary>
	internal static class PrefabVariantValidator {

		private const string PREFAB_ROOT = "Assets/[An-Labo.Virtual]/An-Labo Nail Tool/Resource/Nail/Prefab";

		[MenuItem("An-Labo/Develop/Validate Variant Prefabs")]
		private static void Validate() {
			var sb = new StringBuilder();
			sb.AppendLine("=== Variant Prefab Validation ===");
			sb.AppendLine();

			int errorCount = 0;
			int warnCount = 0;

			// shop.json からバリアント定義を読み込む
			var variants = LoadAllVariantDefinitions();
			sb.AppendLine($"DB上のバリアント定義数: {variants.Count}");
			sb.AppendLine();

			foreach (var v in variants) {
				string header = $"[{v.AvatarName} / {v.VariationName}] Variant '{v.Name}'";

				// ---- GUID チェック ----
				string guidPath = AssetDatabase.GUIDToAssetPath(v.Guid);
				bool guidValid = !string.IsNullOrEmpty(guidPath) && File.Exists(Path.GetFullPath(guidPath));

				if (!guidValid) {
					sb.AppendLine($"  ERROR: {header}");
					sb.AppendLine($"    GUID {v.Guid} が解決できません");
					errorCount++;
				} else {
					// GUID で見つかったファイルの命名規則チェック
					string fileName = Path.GetFileNameWithoutExtension(guidPath);
					string dirName = Path.GetFileName(Path.GetDirectoryName(guidPath) ?? "");

					// 期待: [ShapeName]VariantName.prefab
					var m = Regex.Match(fileName, @"^\[(?<shape>.+)\](?<body>.+)$");
					if (!m.Success) {
						sb.AppendLine($"  WARN: {header}");
						sb.AppendLine($"    ファイル名がネイルプレハブの命名規則に合いません: {fileName}");
						warnCount++;
					} else {
						string body = m.Groups["body"].Value;
						if (!string.Equals(body, v.Name, System.StringComparison.OrdinalIgnoreCase)) {
							// [ShapeName]Avatar_VariantName のような誤った命名
							sb.AppendLine($"  WARN: {header}");
							sb.AppendLine($"    ファイル名が不正です: {fileName}.prefab");
							sb.AppendLine($"    期待: [{m.Groups["shape"].Value}]{v.Name}.prefab");
							warnCount++;
						}
					}

					// フォルダ配下チェック: VariantName フォルダの下にあるべき
					if (!string.Equals(dirName, v.Name, System.StringComparison.OrdinalIgnoreCase)) {
						sb.AppendLine($"  WARN: {header}");
						sb.AppendLine($"    フォルダ名が不一致: {dirName}/ (期待: {v.Name}/)");
						sb.AppendLine($"    パス: {guidPath}");
						warnCount++;
					}
				}

				// ---- ファイル名ベース検索チェック ----
				// Prefabフォルダ内に [*]VariantName.prefab があるか
				bool foundByName = false;
				string fullPrefabRoot = Path.GetFullPath(PREFAB_ROOT);
				if (Directory.Exists(fullPrefabRoot)) {
					try {
						// すべてのシェイプで確認
						foreach (string file in Directory.EnumerateFiles(fullPrefabRoot, $"*{v.Name}.prefab", SearchOption.AllDirectories)) {
							string fn = Path.GetFileNameWithoutExtension(file);
							var fm = Regex.Match(fn, @"^\[.+\](.+)$");
							if (fm.Success && string.Equals(fm.Groups[1].Value, v.Name, System.StringComparison.OrdinalIgnoreCase)) {
								foundByName = true;
								break;
							}
						}
					} catch { /* skip */ }
				}

				if (!foundByName && !guidValid) {
					sb.AppendLine($"  ERROR: {header}");
					sb.AppendLine($"    GUIDでもファイル名でも見つかりません");
					sb.AppendLine($"    Prefabフォルダに [ShapeName]{v.Name}.prefab を配置してください");
					errorCount++;
				} else if (!foundByName && guidValid) {
					sb.AppendLine($"  INFO: {header}");
					sb.AppendLine($"    GUIDでは見つかりますが、正しい命名のファイルがありません");
					sb.AppendLine($"    GUID変更に備えて [ShapeName]{v.Name}.prefab にリネームを推奨");
				}
			}

			// ---- Prefabフォルダ内の孤立ファイルチェック ----
			sb.AppendLine();
			sb.AppendLine("=== 孤立ファイルチェック ===");
			var knownVariantNames = new HashSet<string>(variants.Select(v => v.Name), System.StringComparer.OrdinalIgnoreCase);
			string fullRoot = Path.GetFullPath(PREFAB_ROOT);
			if (Directory.Exists(fullRoot)) {
				foreach (string dir in Directory.EnumerateDirectories(fullRoot)) {
					string dirName = Path.GetFileName(dir);
					// このフォルダ名がどのバリアント名にもマッチしないなら孤立候補
					bool matched = knownVariantNames.Contains(dirName);
					if (!matched) {
						// Avatar_VariantName 形式かチェック
						foreach (string vn in knownVariantNames) {
							if (dirName.EndsWith($"_{vn}", System.StringComparison.OrdinalIgnoreCase)
								|| dirName.EndsWith($".{vn}", System.StringComparison.OrdinalIgnoreCase)) {
								sb.AppendLine($"  WARN: フォルダ '{dirName}' は '{vn}' にリネームすべきかもしれません");
								warnCount++;
								matched = true;
								break;
							}
						}
					}
				}
			}

			sb.AppendLine();
			sb.AppendLine($"=== 結果: エラー {errorCount}件, 警告 {warnCount}件 ===");

			string result = sb.ToString();
			Debug.Log(result);

			// ファイルにも出力
			string outputPath = "Assets/[An-Labo.Virtual]/An-Labo Nail Tool/variant_validation_report.txt";
			string? outputDir = Path.GetDirectoryName(outputPath);
			if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
				Directory.CreateDirectory(outputDir);
			File.WriteAllText(outputPath, result, Encoding.UTF8);
			AssetDatabase.ImportAsset(outputPath);
			Debug.Log($"レポートを出力しました: {outputPath}");
			EditorUtility.DisplayDialog("Variant Validation", $"エラー {errorCount}件, 警告 {warnCount}件\n詳細: {outputPath}", "OK");
		}

		private class VariantInfo {
			public string AvatarName = "";
			public string VariationName = "";
			public string Name = "";
			public string Guid = "";
		}

		private static List<VariantInfo> LoadAllVariantDefinitions() {
			var results = new List<VariantInfo>();

			// ZIP内のshop.jsonを読む
			string? realZipPath = ResourceAutoExtractor.GetZipRealPath();
			if (string.IsNullOrEmpty(realZipPath)) {
				Debug.LogWarning("[Validator] resource.zip.bytes が見つかりません");
				return results;
			}

			try {
				using var archive = System.IO.Compression.ZipFile.OpenRead(realZipPath!);
				var entry = archive.GetEntry("DB/shop.json");
				if (entry == null) return results;

				using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
				string json = reader.ReadToEnd();
				var root = JObject.Parse(json);

				foreach (var shopProp in root.Properties()) {
					var shop = shopProp.Value as JObject;
					if (shop == null) continue;
					var avatars = shop["avatars"] as JObject;
					if (avatars == null) continue;

					foreach (var avProp in avatars.Properties()) {
						var avatar = avProp.Value as JObject;
						if (avatar == null) continue;
						string avatarName = avatar["avatarName"]?.ToString() ?? avProp.Name;

						var variations = avatar["variations"] as JObject;
						if (variations == null) continue;

						foreach (var varProp in variations.Properties()) {
							var variation = varProp.Value as JObject;
							if (variation == null) continue;
							string variationName = variation["variationName"]?.ToString() ?? varProp.Name;

							var bsVariants = variation["blendShapeVariants"] as JArray;
							if (bsVariants == null) continue;

							foreach (var bsv in bsVariants) {
								results.Add(new VariantInfo {
									AvatarName = avatarName,
									VariationName = variationName,
									Name = bsv["name"]?.ToString() ?? "",
									Guid = bsv["nailPrefabGUID"]?.ToString() ?? ""
								});
							}
						}
					}
				}
			} catch (System.Exception e) {
				Debug.LogWarning($"[Validator] shop.json の読み込みに失敗: {e.Message}");
			}

			return results;
		}
	}
}
#endif

using System;
using System.IO;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Model;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public partial class NailSetupProcessor {
		// デバッグ用診断情報 (バージョン / 着用設定 / アバター情報)。
		private string BuildDiagnosticInfo(bool includeFolder = false)
		{
			bool isJa = LanguageManager.CurrentLanguageData.language == "ja";
			var sb = new System.Text.StringBuilder();
			sb.AppendLine();
			sb.AppendLine(isJa ? "--- 診断情報 ---" : "--- Diagnostic Info ---");

			try { sb.AppendLine($"NailTool Version: {MDNailToolDefines.Version}"); }
			catch { sb.AppendLine(isJa ? "NailTool Version: (取得失敗)" : "NailTool Version: (unavailable)"); }

			sb.AppendLine($"ModularAvatar: {GetModularAvatarVersion()}");

			sb.AppendLine($"Avatar: {this.Avatar?.gameObject?.name ?? "(null)"}");
			sb.AppendLine($"Avatar Root Scale: {this.Avatar?.transform?.localScale.ToString() ?? "(null)"}");
			sb.AppendLine($"AvatarName: {this.AvatarName ?? (isJa ? "(未設定)" : "(not set)")}");
			sb.AppendLine($"Variation: {this.AvatarVariationData?.VariationName ?? "(null)"}");
			sb.AppendLine($"NailShape: {this.NailShapeName}");
			// NailPrefab は Process 中で getPrefabPrefix() 後に destroy される (in-memory orphan 残留防止).
			// destroyed 状態でも参照は != null だが name で MissingReferenceException が出るため安全アクセス.
			string nailPrefabName;
			try { nailPrefabName = this.NailPrefab != null ? this.NailPrefab.name : "(null)"; }
			catch (UnityEngine.MissingReferenceException) { nailPrefabName = "(destroyed)"; }
			sb.AppendLine($"NailPrefab: {nailPrefabName}");
			sb.AppendLine($"ForModularAvatar: {this.ForModularAvatar}");
			sb.AppendLine($"BakeBlendShapes: {this.BakeBlendShapes}");
			sb.AppendLine($"SyncBlendShapesWithMA: {this.SyncBlendShapesWithMA}");
			sb.AppendLine($"ArmatureScaleCompensation: {this.ArmatureScaleCompensation}");
			sb.AppendLine($"UseFootNail: {this.UseFootNail}");
			sb.AppendLine($"GenerateMaterial: {this.GenerateMaterial}");

			if (includeFolder)
			{
				string listing = ListPrefabFolderContents();
				if (!string.IsNullOrEmpty(listing))
				{
					sb.AppendLine(isJa ? "--- Resourceフォルダ内容 ---" : "--- Resource Folder Contents ---");
					sb.Append(listing);
				}
			}

			return sb.ToString();
		}

		private static string GetModularAvatarVersion()
		{
			try
			{
				string packageJsonPath = "Packages/nadena.dev.modular-avatar/package.json";
				TextAsset? packageJson = MDNailToolAssetLoader.LoadAssetSafe<TextAsset>(packageJsonPath);
				if (packageJson != null)
				{
					var json = Newtonsoft.Json.Linq.JObject.Parse(packageJson.text);
					return json["version"]?.ToString() ?? "unknown";
				}
			}
			catch { /* ignore */ }

			return "not installed";
		}

		// Nail/Prefab フォルダ内 .prefab 一覧 (デバッグ用)。
		private static string ListPrefabFolderContents()
		{
			string[] searchRoots = {
				"Assets/[An-Labo.Virtual]/An-Labo Nail Tool/Resource/Nail/Prefab",
				"Packages/world.anlabo.mdnailtool/Resource/Nail/Prefab"
			};

			var sb = new System.Text.StringBuilder();
			foreach (string root in searchRoots)
			{
				string fullRoot = Path.GetFullPath(root);
				if (!Directory.Exists(fullRoot)) continue;

				sb.AppendLine($"[{root}]");
				try
				{
					ListPrefabFolderRecursive(fullRoot, fullRoot, sb, 1);
				}
				catch (Exception e)
				{
					sb.AppendLine($"  (読み取りエラー: {e.Message})");
				}
			}
			return sb.ToString();
		}

		private static void ListPrefabFolderRecursive(string current, string root, System.Text.StringBuilder sb, int depth)
		{
			string indent = new string(' ', depth * 2);
			foreach (string dir in Directory.GetDirectories(current))
			{
				string dirName = Path.GetFileName(dir);
				sb.AppendLine($"{indent}{dirName}/");
				ListPrefabFolderRecursive(dir, root, sb, depth + 1);
			}
			foreach (string prefab in Directory.GetFiles(current, "*.prefab"))
			{
				sb.AppendLine($"{indent}{Path.GetFileName(prefab)}");
			}
		}
	}
}

using System;
using System.IO;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Language;

#nullable enable

namespace world.anlabo.mdnailtool.Editor
{
	internal sealed class DiagContext
	{
		internal VRCAvatarDescriptor? Avatar;
		internal string? AvatarName;
		internal AvatarVariation? Variation;
		internal string? NailShapeName;
		internal GameObject? NailPrefab;
		internal bool ForModularAvatar;
		internal bool BakeBlendShapes;
		internal bool SyncBlendShapesWithMA;
		internal bool ArmatureScaleCompensation;
		internal bool UseFootNail;
		internal bool GenerateMaterial;
	}

	internal static class NailToolDiagnosticReport
	{
		internal static string Build(DiagContext ctx, bool includeFolder = false)
		{
			bool isJa = LanguageManager.CurrentLanguageData.language == "ja";
			var sb = new System.Text.StringBuilder();
			sb.AppendLine();
			sb.AppendLine(isJa ? "--- 診断情報 ---" : "--- Diagnostic Info ---");

			try { sb.AppendLine($"NailTool Version: {MDNailToolDefines.Version}"); }
			catch { sb.AppendLine(isJa ? "NailTool Version: (取得失敗)" : "NailTool Version: (unavailable)"); }

			sb.AppendLine($"ModularAvatar: {GetModularAvatarVersion()}");

			sb.AppendLine($"Avatar: {ctx.Avatar?.gameObject?.name ?? "(null)"}");
			sb.AppendLine($"Avatar Root Scale: {ctx.Avatar?.transform?.localScale.ToString() ?? "(null)"}");
			sb.AppendLine($"AvatarName: {ctx.AvatarName ?? (isJa ? "(未設定)" : "(not set)")}");
			sb.AppendLine($"Variation: {ctx.Variation?.VariationName ?? "(null)"}");
			sb.AppendLine($"NailShape: {ctx.NailShapeName}");
			sb.AppendLine($"NailPrefab: {ctx.NailPrefab?.name ?? "(null)"}");
			sb.AppendLine($"ForModularAvatar: {ctx.ForModularAvatar}");
			sb.AppendLine($"BakeBlendShapes: {ctx.BakeBlendShapes}");
			sb.AppendLine($"SyncBlendShapesWithMA: {ctx.SyncBlendShapesWithMA}");
			sb.AppendLine($"ArmatureScaleCompensation: {ctx.ArmatureScaleCompensation}");
			sb.AppendLine($"UseFootNail: {ctx.UseFootNail}");
			sb.AppendLine($"GenerateMaterial: {ctx.GenerateMaterial}");

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
				TextAsset? packageJson = MDNailToolAssetLoader.LoadAssetSafe<TextAsset>("Packages/nadena.dev.modular-avatar/package.json");
				if (packageJson != null)
				{
					var json = Newtonsoft.Json.Linq.JObject.Parse(packageJson.text);
					return json["version"]?.ToString() ?? "unknown";
				}
			}
			catch { /* ignore */ }
			return "not installed";
		}

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

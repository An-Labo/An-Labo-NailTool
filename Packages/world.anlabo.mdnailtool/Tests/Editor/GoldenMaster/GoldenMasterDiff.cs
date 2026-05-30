using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Tests.GoldenMaster
{
	internal static class GoldenMasterDiff
	{
		// BlendShape 重みは Bake 時の浮動小数誤差を許容するため絶対差 0.01 まで許容
		private const float BlendShapeWeightTolerance = 0.01f;

		internal class Report
		{
			public List<string> NodeAdded = new();
			public List<string> NodeRemoved = new();
			public List<string> ComponentChanged = new();
			public List<string> MeshChanged = new();
			public List<string> MaterialChanged = new();
			public List<string> BlendShapeChanged = new();
			public List<string> WarningAdded = new();
			public List<string> WarningRemoved = new();
			public List<string> ErrorAdded = new();
			public List<string> ErrorRemoved = new();
			public string? ExceptionChanged;

			public bool HasAnyDifference =>
				NodeAdded.Count > 0 || NodeRemoved.Count > 0 ||
				ComponentChanged.Count > 0 || MeshChanged.Count > 0 ||
				MaterialChanged.Count > 0 || BlendShapeChanged.Count > 0 ||
				WarningAdded.Count > 0 || WarningRemoved.Count > 0 ||
				ErrorAdded.Count > 0 || ErrorRemoved.Count > 0 ||
				ExceptionChanged != null;

			public int TotalCount =>
				NodeAdded.Count + NodeRemoved.Count + ComponentChanged.Count +
				MeshChanged.Count + MaterialChanged.Count + BlendShapeChanged.Count +
				WarningAdded.Count + WarningRemoved.Count +
				ErrorAdded.Count + ErrorRemoved.Count +
				(ExceptionChanged != null ? 1 : 0);
		}

		internal static Report Compare(GoldenMasterSerializer.Snapshot baseline, GoldenMasterSerializer.Snapshot after)
		{
			Report report = new();

			// JSON 手編集や異常メッシュで重複キーが入っていても RunDiff 全体が例外落ちしないよう, 同一キーは先勝ちで弾く
			Dictionary<string, GoldenMasterSerializer.NodeRecord> baseNodes = ToFirstWinsDict(baseline.Nodes, n => n.Path, "baseline.Nodes");
			Dictionary<string, GoldenMasterSerializer.NodeRecord> afterNodes = ToFirstWinsDict(after.Nodes, n => n.Path, "after.Nodes");

			foreach (string path in afterNodes.Keys)
			{
				if (!baseNodes.ContainsKey(path)) report.NodeAdded.Add(path);
			}
			foreach (string path in baseNodes.Keys)
			{
				if (!afterNodes.ContainsKey(path)) report.NodeRemoved.Add(path);
			}

			foreach (string path in baseNodes.Keys)
			{
				if (!afterNodes.TryGetValue(path, out var afterNode)) continue;
				GoldenMasterSerializer.NodeRecord baseNode = baseNodes[path];

				if (!baseNode.ComponentTypes.SequenceEqual(afterNode.ComponentTypes))
				{
					string b = string.Join(",", baseNode.ComponentTypes);
					string a = string.Join(",", afterNode.ComponentTypes);
					report.ComponentChanged.Add($"{path}: [{b}] -> [{a}]");
				}

				if (baseNode.MeshGuid != afterNode.MeshGuid)
				{
					report.MeshChanged.Add($"{path}: {baseNode.MeshGuid ?? "(null)"} -> {afterNode.MeshGuid ?? "(null)"}");
				}

				if (!baseNode.MaterialGuids.SequenceEqual(afterNode.MaterialGuids))
				{
					string b = string.Join(",", baseNode.MaterialGuids);
					string a = string.Join(",", afterNode.MaterialGuids);
					report.MaterialChanged.Add($"{path}: [{b}] -> [{a}]");
				}
			}

			Dictionary<string, GoldenMasterSerializer.BlendShapeRecord> baseBs =
				ToFirstWinsDict(baseline.BlendShapes, b => $"{b.SmrPath}::{b.BlendShapeName}", "baseline.BlendShapes");
			Dictionary<string, GoldenMasterSerializer.BlendShapeRecord> afterBs =
				ToFirstWinsDict(after.BlendShapes, b => $"{b.SmrPath}::{b.BlendShapeName}", "after.BlendShapes");

			foreach (string key in baseBs.Keys)
			{
				if (!afterBs.TryGetValue(key, out var afterRec))
				{
					report.BlendShapeChanged.Add($"{key}: removed");
					continue;
				}
				float diff = System.Math.Abs(baseBs[key].Weight - afterRec.Weight);
				if (diff > BlendShapeWeightTolerance)
				{
					report.BlendShapeChanged.Add($"{key}: {baseBs[key].Weight:F4} -> {afterRec.Weight:F4} (diff={diff:F4})");
				}
			}
			foreach (string key in afterBs.Keys)
			{
				if (!baseBs.ContainsKey(key)) report.BlendShapeChanged.Add($"{key}: added");
			}

			HashSet<string> baseWarn = new(baseline.Warnings);
			HashSet<string> afterWarn = new(after.Warnings);
			foreach (string w in afterWarn) if (!baseWarn.Contains(w)) report.WarningAdded.Add(w);
			foreach (string w in baseWarn) if (!afterWarn.Contains(w)) report.WarningRemoved.Add(w);

			HashSet<string> baseErr = new(baseline.Errors);
			HashSet<string> afterErr = new(after.Errors);
			foreach (string e in afterErr) if (!baseErr.Contains(e)) report.ErrorAdded.Add(e);
			foreach (string e in baseErr) if (!afterErr.Contains(e)) report.ErrorRemoved.Add(e);

			if (baseline.ProcessException != after.ProcessException)
			{
				report.ExceptionChanged = $"{baseline.ProcessException ?? "(none)"} -> {after.ProcessException ?? "(none)"}";
			}

			return report;
		}

		internal static string FormatReport(Report report, string baselineId, string afterId)
		{
			System.Text.StringBuilder sb = new();
			sb.AppendLine($"=== GoldenMaster Diff: {baselineId} vs {afterId} ===");
			sb.AppendLine($"Total differences: {report.TotalCount}");

			AppendSection(sb, "Node added", report.NodeAdded);
			AppendSection(sb, "Node removed", report.NodeRemoved);
			AppendSection(sb, "Component changed", report.ComponentChanged);
			AppendSection(sb, "Mesh changed", report.MeshChanged);
			AppendSection(sb, "Material changed", report.MaterialChanged);
			AppendSection(sb, $"BlendShape changed (tolerance={BlendShapeWeightTolerance})", report.BlendShapeChanged);
			AppendSection(sb, "Warning added", report.WarningAdded);
			AppendSection(sb, "Warning removed", report.WarningRemoved);
			AppendSection(sb, "Error added", report.ErrorAdded);
			AppendSection(sb, "Error removed", report.ErrorRemoved);
			if (report.ExceptionChanged != null)
			{
				sb.AppendLine($"[Exception changed] {report.ExceptionChanged}");
			}

			return sb.ToString();
		}

		private static void AppendSection(System.Text.StringBuilder sb, string title, List<string> items)
		{
			if (items.Count == 0) return;
			sb.AppendLine($"[{title}] {items.Count}");
			foreach (string item in items) sb.AppendLine($"  - {item}");
		}

		private static Dictionary<string, T> ToFirstWinsDict<T>(IEnumerable<T> source, System.Func<T, string> keySelector, string sourceLabel)
		{
			Dictionary<string, T> dict = new();
			foreach (T item in source)
			{
				string key = keySelector(item);
				if (dict.ContainsKey(key))
				{
					ToolConsole.Warn("GoldenMaster", $"{sourceLabel}: 重複キー {key} を検出. 先勝ちで採用");
					continue;
				}
				dict.Add(key, item);
			}
			return dict;
		}
	}
}

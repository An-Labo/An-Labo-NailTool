#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using world.anlabo.mdnailtool.Editor.Language;

namespace world.anlabo.mdnailtool.Editor.VisualElements
{
	internal sealed class UpdateNoticeBanner : VisualElement
	{
		private const string PackageName = "world.anlabo.mdnailtool";
		private const string RepositoryUrl = "https://An-Labo.github.io/An-Labo-NailTool/vpm.json";
		private const bool ForceShowForPreview = false;
		private const double SuccessCacheSeconds = 3600d;
		private const double FailureRetrySeconds = 60d;
		private static bool _checking;
		private static string? _latestVersion;
		private static bool _hasUpdate;
		private static double _nextCheckAt;
		private static int _cacheGeneration;
		private static event Action? ResultChanged;

		internal static void ClearCache()
		{
			_cacheGeneration++;
			_checking = false;
			_nextCheckAt = 0;
			_latestVersion = null;
			_hasUpdate = false;
			ResultChanged?.Invoke();
		}

		public UpdateNoticeBanner()
		{
			AddToClassList("mdn-update-notice");
			style.display = DisplayStyle.None;
			var titleLabel = new Label(LanguageManager.S("window.update_available_title") ?? "Update available");
			titleLabel.AddToClassList("mdn-update-notice-title");
			Add(titleLabel);
			RegisterCallback<AttachToPanelEvent>(_ => {
				ResultChanged -= ApplyResult;
				ResultChanged += ApplyResult;
				CheckForUpdate();
				ApplyResult();
			});
			RegisterCallback<DetachFromPanelEvent>(_ => ResultChanged -= ApplyResult);
			// Panelから外れるとschedulerは止まる。開いたままでも復帰後に再確認する。
			schedule.Execute(() => { CheckForUpdate(); ApplyResult(); }).Every(30000);
			CheckForUpdate();
			ApplyResult();
		}

		private static void CheckForUpdate()
		{
			if (_checking || EditorApplication.timeSinceStartup < _nextCheckAt) return;
			_checking = true;
			int generation = _cacheGeneration;
			UnityWebRequest? request = null;
			UnityWebRequestAsyncOperation operation;
			double startedAt = EditorApplication.timeSinceStartup;
			try {
				request = UnityWebRequest.Get(RepositoryUrl);
				request.timeout = 10;
				operation = request.SendWebRequest();
			} catch (Exception ex) {
				request?.Dispose();
				RecordFailure(ex.Message);
				return;
			}

			void Poll()
			{
				// ClearCache後の古い照会は新しい結果やcheckingフラグを上書きしない。
				if (generation != _cacheGeneration) {
					EditorApplication.update -= Poll;
					request.Abort();
					request.Dispose();
					return;
				}
				if (!operation.isDone && EditorApplication.timeSinceStartup - startedAt < 15d) return;
				EditorApplication.update -= Poll;
				try {
					if (!operation.isDone) request.Abort();
					if (request.result != UnityWebRequest.Result.Success) throw new InvalidOperationException(request.error ?? "Update request failed");
					RecordSuccess(request.downloadHandler.text, generation);
				} catch (Exception ex) {
					RecordFailure(ex.Message);
				} finally {
					request.Dispose();
					ResultChanged?.Invoke();
				}
			}
			EditorApplication.update += Poll;
		}

		internal static bool RecordSuccess(string json, int generation) {
			if (generation != _cacheGeneration) return false;
			string? latest = ExtractLatestVersionForCurrent(json, MDNailToolDefines.Version);
			if (latest == null) throw new InvalidOperationException("No valid package version in update response");
			// 検証完了後にのみ成功cacheを更新する。古い応答・不正JSONは現状態を触らない。
			_latestVersion = latest;
			_hasUpdate = IsRemoteNewer(latest, MDNailToolDefines.Version);
			_checking = false;
			_nextCheckAt = EditorApplication.timeSinceStartup + SuccessCacheSeconds;
			return true;
		}

		private static void RecordFailure(string message) {
			_checking = false;
			_nextCheckAt = EditorApplication.timeSinceStartup + FailureRetrySeconds;
			// 過去の成功結果は通信障害だけでは消さない。
			ToolConsole.Warn("NailTool", $"更新確認に失敗（後で再試行）: {message}");
		}

		private void ApplyResult() {
			style.display = ForceShowForPreview || (_hasUpdate && !string.IsNullOrEmpty(_latestVersion)) ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private static string? ExtractLatestVersion(string json) => ExtractLatestVersionForCurrent(json, MDNailToolDefines.Version);

		internal static string? ExtractLatestVersionForCurrent(string json, string current) {
			JObject root = JObject.Parse(json);
			if (root["packages"]?[PackageName]?["versions"] is not JObject versions) return null;
			bool includePrerelease = TryParseVersion(current, out var local) && local.Prerelease.Length > 0;
			string? bestText = null;
			SemanticVersion? best = null;
			foreach (var property in versions.Properties()) {
				if (!TryParseVersion(property.Name, out var parsed)) continue;
				if (!includePrerelease && parsed.Prerelease.Length > 0) continue;
				if (best == null || parsed.CompareTo(best) > 0) { best = parsed; bestText = property.Name; }
			}
			return bestText;
		}

		internal static bool IsRemoteNewer(string? remote, string current) =>
			TryParseVersion(remote, out var r) && TryParseVersion(current, out var c) && r.CompareTo(c) > 0;

		private static bool TryParseVersion(string? text, out SemanticVersion parsed) {
			parsed = null!;
			if (string.IsNullOrWhiteSpace(text)) return false;
			var m = Regex.Match(text!.Trim(), @"^[vV]?(?<a>0|[1-9]\d*)\.(?<b>0|[1-9]\d*)\.(?<c>0|[1-9]\d*)(?:-(?<pre>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$");
			if (!m.Success) return false;
			string pre = m.Groups["pre"].Value;
			if (pre.Split('.').Any(p => p.Length > 1 && p.All(char.IsDigit) && p[0] == '0')) return false;
			parsed = new SemanticVersion(new[] {m.Groups["a"].Value, m.Groups["b"].Value, m.Groups["c"].Value}, pre);
			return true;
		}

		private sealed class SemanticVersion : IComparable<SemanticVersion> {
			private readonly string[] _core;
			public readonly string Prerelease;
			public SemanticVersion(string[] core, string prerelease) { _core = core; Prerelease = prerelease; }
			public int CompareTo(SemanticVersion? other) {
				if (other == null) return 1;
				for (int i = 0; i < 3; i++) { int n = CompareNumber(_core[i], other._core[i]); if (n != 0) return n; }
				if (Prerelease.Length == 0 || other.Prerelease.Length == 0) return (Prerelease.Length == 0 ? 1 : 0).CompareTo(other.Prerelease.Length == 0 ? 1 : 0);
				string[] a = Prerelease.Split('.'), b = other.Prerelease.Split('.');
				for (int i = 0; i < Math.Min(a.Length, b.Length); i++) {
					bool an = a[i].All(char.IsDigit), bn = b[i].All(char.IsDigit);
					int n = an && bn ? CompareNumber(a[i], b[i]) : an != bn ? (an ? -1 : 1) : string.CompareOrdinal(a[i], b[i]);
					if (n != 0) return n;
				}
				return a.Length.CompareTo(b.Length);
			}
			private static int CompareNumber(string a, string b) => a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
		}
	}
}

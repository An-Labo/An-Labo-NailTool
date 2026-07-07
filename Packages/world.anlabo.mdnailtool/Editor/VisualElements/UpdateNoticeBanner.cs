#nullable enable

using System;
using System.Linq;
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

		private static bool _checked;
		private static string? _latestVersion;
		private static bool _hasUpdate;

		public UpdateNoticeBanner()
		{
			AddToClassList("mdn-update-notice");
			style.display = DisplayStyle.None;

			var titleLabel = new Label(LanguageManager.S("window.update_available_title") ?? "Update available");
			titleLabel.AddToClassList("mdn-update-notice-title");
			Add(titleLabel);

			if (_checked)
			{
				ApplyResult();
			}
			else
			{
				CheckForUpdate();
			}
		}

		private void CheckForUpdate()
		{
			UnityWebRequest request = UnityWebRequest.Get(RepositoryUrl);
			request.timeout = 10;
			UnityWebRequestAsyncOperation operation = request.SendWebRequest();
			double startedAt = EditorApplication.timeSinceStartup;

			void Poll()
			{
				if (!operation.isDone && EditorApplication.timeSinceStartup - startedAt < 15d) return;
				EditorApplication.update -= Poll;

				try
				{
					_checked = true;
					if (request.result == UnityWebRequest.Result.Success)
					{
						_latestVersion = ExtractLatestVersion(request.downloadHandler.text);
						_hasUpdate = IsRemoteNewer(_latestVersion, MDNailToolDefines.Version);
					}
				}
				catch (Exception ex)
				{
					ToolConsole.Warn("NailTool", $"更新確認に失敗: {ex.Message}");
					_checked = true;
					_hasUpdate = false;
				}
				finally
				{
					request.Dispose();
					ApplyResult();
				}
			}

			EditorApplication.update += Poll;
		}

		private void ApplyResult()
		{
			if (!ForceShowForPreview && (!_hasUpdate || string.IsNullOrEmpty(_latestVersion)))
			{
				style.display = DisplayStyle.None;
				return;
			}

			style.display = DisplayStyle.Flex;
		}

		private static string? ExtractLatestVersion(string json)
		{
			JObject root = JObject.Parse(json);
			JToken? versions = root["packages"]?[PackageName]?["versions"];
			if (versions is not JObject versionObject) return null;

			return versionObject.Properties()
				.Select(p => p.Name)
				.Where(v => TryParseVersion(v, out _))
				.OrderByDescending(v => ParseVersion(v))
				.FirstOrDefault();
		}

		private static bool IsRemoteNewer(string? remote, string current)
		{
			if (!TryParseVersion(remote, out Version remoteVersion)) return false;
			return TryParseVersion(current, out Version currentVersion) && remoteVersion > currentVersion;
		}

		private static Version ParseVersion(string version)
		{
			TryParseVersion(version, out Version parsed);
			return parsed;
		}

		private static bool TryParseVersion(string? version, out Version parsed)
		{
			parsed = new Version(0, 0, 0);
			if (string.IsNullOrWhiteSpace(version)) return false;

			string normalized = version!.Trim().TrimStart('v', 'V');
			int suffixIndex = normalized.IndexOfAny(new[] { '-', '+' });
			if (suffixIndex >= 0) normalized = normalized.Substring(0, suffixIndex);
			return Version.TryParse(normalized, out parsed);
		}
	}
}

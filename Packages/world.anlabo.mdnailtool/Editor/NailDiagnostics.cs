using System;
using System.Runtime.CompilerServices;

#nullable enable

namespace world.anlabo.mdnailtool.Editor
{
	// ToolConsole の subsystem を NailDiag/<category> に統一する wrapper。既存ログ文言は触らず、新規ログから経由する運用。
	internal static class NailDiagnostics
	{
		internal static bool VerboseEnabled = false;

		internal static void Warn(string category, string message,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
		{
			ToolConsole.Warn($"NailDiag/{category}", message, file, method, line);
		}

		internal static void Error(string category, string message, Exception? cause = null,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
		{
			ToolConsole.Error($"NailDiag/{category}", message, cause, file, method, line);
		}

		internal static void Info(string category, string message,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
		{
			ToolConsole.Info($"NailDiag/{category}", message, file, method, line);
		}

		// VerboseEnabled=true の時のみ Info として出力する。release 時はバッファに残らない。
		internal static void Verbose(string category, string message,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
		{
			if (!VerboseEnabled) return;
			ToolConsole.Info($"NailDiag/{category}", message, file, method, line);
		}

		internal static void ResourceMissing(string resourcePath, string? context = null,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
		{
			string msg = context != null
				? $"resource missing: {resourcePath} ({context})"
				: $"resource missing: {resourcePath}";
			ToolConsole.Warn("NailDiag/Resource", msg, file, method, line);
		}

		internal static void AssetResolveFailed(string assetKey, string reason,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
		{
			ToolConsole.Warn("NailDiag/AssetResolve", $"failed to resolve '{assetKey}': {reason}", file, method, line);
		}
	}
}

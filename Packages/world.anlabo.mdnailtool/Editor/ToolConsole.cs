using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

#nullable enable

namespace world.anlabo.mdnailtool.Editor
{
	/// <summary>
	/// ツール内Consoleへのログ出力を管理する静的クラス。
	/// UIが接続されていない場合はバッファに蓄積し、接続時にフラッシュする。
	/// </summary>
	internal static class ToolConsole
	{
		private static readonly List<string> Buffer = new();
		internal static Action<string>? OnLog;

		internal static void Log(string message)
		{
			string line = $"[{DateTime.Now:HH:mm:ss}] {message}";

			// Unity Console には Error/Warning のみ出力 (通常 Log は UI Console のみ).
			// 行頭 prefix で判定する (e.Message に [Error] が含まれる等の誤判定を回避).
			if (message.StartsWith("[Error]") || message.StartsWith("[NailDiag][Error]"))
				UnityEngine.Debug.LogError(line);
			else if (message.StartsWith("[Warning]") || message.StartsWith("[NailDiag][Warning]"))
				UnityEngine.Debug.LogWarning(line);

			if (OnLog != null)
				OnLog.Invoke(line);
			else
				Buffer.Add(line);
		}

		/// <summary>
		/// バッファに蓄積されたログをフラッシュする。OnLog設定後に呼ぶ。
		/// </summary>
		internal static void Flush()
		{
			if (OnLog == null) return;
			foreach (string line in Buffer) OnLog.Invoke(line);
			Buffer.Clear();
		}

		internal static void Clear()
		{
			Buffer.Clear();
		}

		internal static void Error(string subsystem, string message, Exception? cause = null,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
		{
			string location = FormatLocation(subsystem, file, method, line);
			string formatted = $"[Error]{location} {message}{(cause != null ? $" cause={cause}" : "")}";
			Log(formatted);
		}

		internal static void Warn(string subsystem, string message,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
		{
			Log($"[Warning]{FormatLocation(subsystem, file, method, line)} {message}");
		}

		internal static void Info(string subsystem, string message,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
		{
			Log($"[Info]{FormatLocation(subsystem, file, method, line)} {message}");
		}

		private static string FormatLocation(string subsystem, string file, string method, int line)
		{
			string fileName = string.IsNullOrEmpty(file) ? "?" : Path.GetFileNameWithoutExtension(file);
			return $"[{subsystem}/{fileName}.{method}@{line}]";
		}
	}
}

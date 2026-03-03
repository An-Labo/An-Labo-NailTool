using System;
using System.Collections.Generic;

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
	}
}

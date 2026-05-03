using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public abstract class NailToolException : Exception {
		public string Subsystem { get; }
		public string SourceFile { get; }
		public string SourceMethod { get; }
		public int SourceLine { get; }
		public Dictionary<string, string?> Context { get; } = new();

		protected NailToolException(string subsystem, string message, Exception? cause,
			string file, string method, int line)
			: base(message, cause) {
			this.Subsystem = subsystem;
			this.SourceFile = file;
			this.SourceMethod = method;
			this.SourceLine = line;
		}

		public string FormatLocation() {
			string fileName = string.IsNullOrEmpty(this.SourceFile)
				? "?"
				: Path.GetFileNameWithoutExtension(this.SourceFile);
			return $"[{this.Subsystem}/{fileName}.{this.SourceMethod}@{this.SourceLine}]";
		}

		public string FormatContext() {
			if (this.Context.Count == 0) return "";
			List<string> parts = new();
			foreach (KeyValuePair<string, string?> kv in this.Context) {
				parts.Add($"{kv.Key}={kv.Value ?? "null"}");
			}
			return $" {{{string.Join(", ", parts)}}}";
		}

		public override string ToString() {
			return $"{this.GetType().Name}{this.FormatLocation()} {this.Message}{this.FormatContext()}\n{this.StackTrace}";
		}
	}

	public class NailToolUserException : NailToolException {
		public NailToolUserException(string subsystem, string message, Exception? cause = null,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
			: base(subsystem, message, cause, file, method, line) { }
	}

	public class NailToolDeveloperException : NailToolException {
		public NailToolDeveloperException(string subsystem, string message, Exception? cause = null,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
			: base(subsystem, message, cause, file, method, line) { }
	}

	public class NailToolResourceException : NailToolException {
		public NailToolResourceException(string subsystem, string message, Exception? cause = null,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
			: base(subsystem, message, cause, file, method, line) { }
	}
}

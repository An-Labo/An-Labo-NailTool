using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Report {
	public class ReportGenerator {
		static ReportGenerator() {
			if (Directory.Exists(MDNailToolDefines.REPORT_PATH)) return;
			Directory.CreateDirectory(MDNailToolDefines.REPORT_PATH);
			AssetDatabase.Refresh();
		}

		private string? _cashText;


		public string GetText() {
			if (this._cashText != null) {
				return this._cashText;
			}

			StringBuilder builder = new();

			{
				builder.AppendLine("[Nail List Dump]");
				DBNailDesign dbNailDesign = new();
				foreach (NailDesign nailDesign in dbNailDesign.collection) {
					builder.Append(nailDesign.DesignName);
					builder.Append(" : ");
					bool isInstalled = INailProcessor.IsInstalledDesign(nailDesign.DesignName);
					builder.AppendLine(isInstalled ? "Installed" : "Not Install");
					if (!isInstalled) continue;
					INailProcessor processor = INailProcessor.CreateNailDesign(nailDesign.DesignName);
					processor.ReportDesign(builder);
					if (nailDesign.MaterialVariation == null) {
						foreach (NailColorVariation colorVariation in nailDesign.ColorVariation.Values) {
							builder.Append("  - ");
							builder.Append(colorVariation.ColorName);
							builder.Append(" : ");
							builder.AppendLine(processor.IsInstalledColorVariation(string.Empty, colorVariation.ColorName) ? "Installed" : "Not Install");
							processor.ReportVariation(string.Empty, colorVariation.ColorName, builder);
						}
					} else {
						foreach (NailMaterialVariation materialVariation in nailDesign.MaterialVariation.Values) {
							foreach (NailColorVariation colorVariation in nailDesign.ColorVariation.Values) {
								builder.Append("  - ");
								builder.Append(materialVariation.MaterialName);
								builder.Append(" : ");
								builder.Append(colorVariation.ColorName);
								builder.Append(" : ");
								builder.AppendLine(processor.IsInstalledColorVariation(materialVariation.MaterialName, colorVariation.ColorName) ? "Installed" : "Not Install");
								processor.ReportVariation(materialVariation.MaterialName, colorVariation.ColorName, builder);
							}
						}
					}
				}
			}

			{
				builder.AppendLine("[DirectoryDump]");
				foreach (string directory in this.DumpDirectory("Assets/[An-Labo.Virtual]")) {
					builder.AppendLine(directory);
				}

				foreach (string directory in this.DumpDirectory("Packages/world.anlabo.mdnailtool")) {
					builder.AppendLine(directory);
				}
			}


			this._cashText = builder.ToString();
			return this._cashText;
		}

		public void SaveReportWithDialog() {
			string savePath = EditorUtility.SaveFilePanel("Save Report", MDNailToolDefines.REPORT_PATH, $"Report {DateTime.Now:yyyy-M-d-hh-mm-ss}", "txt");
			if (string.IsNullOrEmpty(savePath)) return;
			File.WriteAllText(savePath, this.GetText());
			AssetDatabase.Refresh();
		}

		private IEnumerable<string> DumpDirectory(string root) {
			Stack<string> stack = new();
			stack.Push(root);

			while (stack.Count > 0) {
				string current = stack.Pop();
				yield return current.Replace('\\', '/') + "/";

				foreach (string file in Directory.EnumerateFiles(current)) {
					yield return file.Replace('\\', '/');
				}

				foreach (string directory in Directory.EnumerateDirectories(current)) {
					stack.Push(directory);
				}
			}
		}
	}
}
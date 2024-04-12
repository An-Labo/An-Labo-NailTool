using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.JsonData;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;

namespace world.anlabo.mdnailtool.Editor {
	public class LegacyDesignInstaller : AssetPostprocessor {
		public static void ReInstallLegacyNail() {
			if (!UnityEngine.Windows.Directory.Exists(MDNailToolDefines.LEGACY_DESIGN_PATH)) return;
			using DBNailDesign dbNailDesign = new();

			foreach (NailDesign nailDesign in dbNailDesign.collection) {
				if (!INailProcessor.IsInstalledDesign(nailDesign.DesignName)) continue;
				INailProcessor processor = INailProcessor.CreateNailDesign(nailDesign.DesignName);
				if (processor.DesignData.Type != DesignData.JsonType.Legacy) continue;
				File.Delete($"{MDNailToolDefines.NAIL_DESIGN_PATH}{nailDesign.DesignName}/_design.json");
				File.Delete($"{MDNailToolDefines.NAIL_DESIGN_PATH}{nailDesign.DesignName}/_design.json.meta");
				Debug.Log($"Uninstall {nailDesign.DesignName}");
			}

			InstallLegacyNail();
		}

		private static void InstallLegacyNail() {
			if (!Directory.Exists(MDNailToolDefines.LEGACY_DESIGN_PATH)) return;
			using DBNailDesign dbNailDesign = new();

			Regex directoryPattern = new("【(?<designName>.+)】");

			foreach (string directory in Directory.EnumerateDirectories(MDNailToolDefines.LEGACY_DESIGN_PATH)) {
				string directoryName = Path.GetFileName(directory);
				Match match = directoryPattern.Match(directoryName);
				if (!match.Success) continue;

				string textureDirectory = $"{directory}/[Data]/[Texture]";
				if (!Directory.Exists(textureDirectory)) continue;

				string designName = match.Groups["designName"].Value;
				NailDesign nailDesign = dbNailDesign.FindNailDesignByDesignName(designName);
				if (nailDesign == null) {
					Debug.Log($"{designName} is not design");
					continue;
				}

				if (INailProcessor.IsInstalledDesign(nailDesign.DesignName)) {
					continue;
				}

				Debug.Log($"Find Legacy NailDesign: {designName}");


				string targetPath = $"{MDNailToolDefines.NAIL_DESIGN_PATH}{designName}/";
				if (!Directory.Exists(targetPath)) {
					Debug.Log($"Not found Legacy design install Directory : {targetPath}");
					continue;
				}

				DesignData designData = new() {
					Type = DesignData.JsonType.Legacy,
					Legacy = new LegacyDesignData {
						DesignDirectoryGUID = AssetDatabase.AssetPathToGUID(directory)
					}
				};

				switch (designName) {
					case "HoroNail": {
						designData.Legacy.AdditionalMaterialGUIDs = new[] { "b301cdf847023624b9d9b3f9b48a78e4" };
						break;
					}
					case "MagicNail": {
						designData.Legacy.AdditionalObjectGUIDs = new Dictionary<MDNailToolDefines.TargetFinger, string[]> {
							{ MDNailToolDefines.TargetFinger.LeftRing, new[] { "6396c8acd4b391f4a872050feedd8b0b" } },
							{ MDNailToolDefines.TargetFinger.RightRing, new[] { "6396c8acd4b391f4a872050feedd8b0b" } }
						};
						break;
					}
				}

				File.WriteAllText($"{targetPath}_design.json", designData.ToJson());
				AssetDatabase.Refresh();
				Debug.Log($"Installed Legacy NailDesign: {designName}");
			}
		}


		private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
			InstallLegacyNail();
		}
	}
}
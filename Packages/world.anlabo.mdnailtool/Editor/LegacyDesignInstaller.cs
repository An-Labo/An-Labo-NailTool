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
					case "LILITHYNail": {
						designData.Legacy.AdditionalObjectGUIDs = new Dictionary<MDNailToolDefines.TargetFinger, string[]> {
							{ MDNailToolDefines.TargetFinger.LeftRing, new[] { "13a4d9bebb685a04bb0f4a797f2b1f47" }},
							{ MDNailToolDefines.TargetFinger.RightRing, new[] { "13a4d9bebb685a04bb0f4a797f2b1f47" }},
						};
						break;
					}
					case "WeatherNail-falling star": {
						designData.Legacy.AdditionalMaterialGUIDs = new[] { "203d5349fc7378f479dd251156ff0a73" };
						break;
					}
					case "WeatherNail-rain": {
						designData.Legacy.AdditionalMaterialGUIDs = new[] { "40cd7558714d88547b4ff556a1438aff",
																			"ee55a6dcda4337642aa17c4aaa559acc",
																			"571f8c9bcd281e94a990aabdff8ba5f3",
																			"c8821d596c605c9459518797f7f5dabc" };
						break;
					}
					case "WeatherNail-snow": {
						designData.Legacy.AdditionalMaterialGUIDs = new[] { "db388f72784d3f241a0f2b5da0a605ce",
																			"310bd38e86d65d1418cd69b389a70b9b",
																			"73eb6039e82e68e4aab95b7201df4158",
																			"9d3b17d5b3af6974693492ea548f2975" };
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
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using world.anlabo.mdnailtool.Editor.NailDesigns;

#nullable enable

namespace world.anlabo.mdnailtool.Editor
{
	internal static class MDNailToolUsageStats
	{
		internal static void Update(
			(INailProcessor, string, string)[] designAndVariationNames,
			string avatarKey,
			string? nailShapeName = null,
			bool isHandActive = true,
			bool isFootActive = false,
			bool useModularAvatar = false,
			string? additionalMaterialSource = null,
			string? additionalObjectSource = null)
		{
			Dictionary<string, DateTime> lastUsedTimes = GlobalSetting.DesignLastUsedTimes;
			Dictionary<string, int> designUsedCounts = GlobalSetting.DesignUseCount;
			Dictionary<string, int> variationUseCount = GlobalSetting.VariationUseCount;
			Dictionary<string, int> avatarUseCount = GlobalSetting.AvatarUseCount;
			Dictionary<string, int> nailShapeUseCount = GlobalSetting.NailShapeUseCount;
			Dictionary<string, int> additionalMaterialUseCount = GlobalSetting.AdditionalMaterialUseCount;
			Dictionary<string, int> additionalObjectUseCount = GlobalSetting.AdditionalObjectUseCount;
			Dictionary<string, int> optionUseCount = GlobalSetting.OptionUseCount;

			HashSet<string> uniqueDesignNames = new();
			HashSet<string> uniqueVariationKeys = new();
			foreach ((INailProcessor nailProcessor, string materialName, string colorName) in designAndVariationNames)
			{
				if (nailProcessor != null && !string.IsNullOrEmpty(nailProcessor.DesignName))
				{
					uniqueDesignNames.Add(nailProcessor.DesignName);

					if (!string.IsNullOrEmpty(materialName) || !string.IsNullOrEmpty(colorName))
					{
						uniqueVariationKeys.Add($"{nailProcessor.DesignName}:{materialName}:{colorName}");
					}
				}
			}

			foreach (string dName in uniqueDesignNames)
			{
				lastUsedTimes[dName] = DateTime.Now;
				designUsedCounts[dName] = designUsedCounts.GetValueOrDefault(dName, 0) + 1;
			}

			foreach (string vKey in uniqueVariationKeys)
			{
				variationUseCount[vKey] = variationUseCount.GetValueOrDefault(vKey, 0) + 1;
			}

			avatarUseCount[avatarKey] = avatarUseCount.GetValueOrDefault(avatarKey, 0) + 1;

			if (!string.IsNullOrEmpty(nailShapeName))
			{
				nailShapeUseCount[nailShapeName!] = nailShapeUseCount.GetValueOrDefault(nailShapeName!, 0) + 1;
			}

			if (!string.IsNullOrEmpty(additionalMaterialSource))
			{
				additionalMaterialUseCount[additionalMaterialSource!] = additionalMaterialUseCount.GetValueOrDefault(additionalMaterialSource!, 0) + 1;
			}

			if (!string.IsNullOrEmpty(additionalObjectSource))
			{
				additionalObjectUseCount[additionalObjectSource!] = additionalObjectUseCount.GetValueOrDefault(additionalObjectSource!, 0) + 1;
			}

			string handFootKey = (isHandActive, isFootActive) switch
			{
				(true, true) => "hand+foot",
				(true, false) => "hand_only",
				(false, true) => "foot_only",
				_ => "none"
			};
			optionUseCount[handFootKey] = optionUseCount.GetValueOrDefault(handFootKey, 0) + 1;

			if (useModularAvatar)
			{
				optionUseCount["modular_avatar"] = optionUseCount.GetValueOrDefault("modular_avatar", 0) + 1;
			}

			GlobalSetting.DesignLastUsedTimes = lastUsedTimes;
			GlobalSetting.DesignUseCount = designUsedCounts;
			GlobalSetting.VariationUseCount = variationUseCount;
			GlobalSetting.AvatarUseCount = avatarUseCount;
			GlobalSetting.NailShapeUseCount = nailShapeUseCount;
			GlobalSetting.AdditionalMaterialUseCount = additionalMaterialUseCount;
			GlobalSetting.AdditionalObjectUseCount = additionalObjectUseCount;
			GlobalSetting.OptionUseCount = optionUseCount;
		}

		internal static void Migrate()
		{
			const string OLD_DESIGN_KEY = "world.anlabo.mdnailtool.design_use_count";
			if (!EditorPrefs.HasKey(OLD_DESIGN_KEY)) return;

			var oldDesignCounts = JsonConvert.DeserializeObject<Dictionary<string, int>>(EditorPrefs.GetString(OLD_DESIGN_KEY));
			if (oldDesignCounts != null)
			{
				var migrated = new Dictionary<string, int>();
				foreach (var kvp in oldDesignCounts) migrated[kvp.Key] = Mathf.CeilToInt(kvp.Value / 24.0f);
				GlobalSetting.DesignUseCount = migrated;
			}

			EditorPrefs.DeleteKey(OLD_DESIGN_KEY);
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.avatar_use_count");
		}
	}
}

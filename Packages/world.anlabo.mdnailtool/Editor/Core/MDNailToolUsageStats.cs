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
		internal static void Update((INailProcessor, string, string)[] designAndVariationNames, string avatarKey)
		{
			Dictionary<string, DateTime> lastUsedTimes = GlobalSetting.DesignLastUsedTimes;
			Dictionary<string, int> designUsedCounts = GlobalSetting.DesignUseCount;
			Dictionary<string, int> avatarUseCount = GlobalSetting.AvatarUseCount;

			HashSet<string> uniqueDesignNames = new();
			foreach ((INailProcessor nailProcessor, string _, string _) in designAndVariationNames)
			{
				if (nailProcessor != null && !string.IsNullOrEmpty(nailProcessor.DesignName))
				{
					uniqueDesignNames.Add(nailProcessor.DesignName);
				}
			}

			foreach (string dName in uniqueDesignNames)
			{
				lastUsedTimes[dName] = DateTime.Now;
				designUsedCounts[dName] = designUsedCounts.GetValueOrDefault(dName, 0) + 1;
			}

			avatarUseCount[avatarKey] = avatarUseCount.GetValueOrDefault(avatarKey, 0) + 1;

			GlobalSetting.DesignLastUsedTimes = lastUsedTimes;
			GlobalSetting.DesignUseCount = designUsedCounts;
			GlobalSetting.AvatarUseCount = avatarUseCount;
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

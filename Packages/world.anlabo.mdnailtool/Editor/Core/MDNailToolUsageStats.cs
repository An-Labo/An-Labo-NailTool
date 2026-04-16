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
			string avatarKey)
		{
			Dictionary<string, DateTime> lastUsedTimes = GlobalSetting.DesignLastUsedTimes;
			Dictionary<string, int> designUsedCounts = GlobalSetting.DesignUseCount;
			Dictionary<string, int> variationUseCount = GlobalSetting.VariationUseCount;
			Dictionary<string, int> avatarUseCount = GlobalSetting.AvatarUseCount;

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

			GlobalSetting.DesignLastUsedTimes = lastUsedTimes;
			GlobalSetting.DesignUseCount = designUsedCounts;
			GlobalSetting.VariationUseCount = variationUseCount;
			GlobalSetting.AvatarUseCount = avatarUseCount;
		}

		internal static void Migrate()
		{
			const string OLD_DESIGN_KEY = "world.anlabo.mdnailtool.design_use_count";
			if (EditorPrefs.HasKey(OLD_DESIGN_KEY))
			{
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

			// 過剰収集だった統計キーを削除
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.nail_shape_count");
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.additional_material_count");
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.additional_object_count");
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.option_count");
		}

		/// <summary>
		/// 着用統計データを全て削除する。
		/// 対象: デザイン/バリエーション/アバター 使用回数、デザイン最終使用日時、
		///       過去バージョンの旧キー、直近のビルド診断ログ。
		/// </summary>
		internal static void ResetAll()
		{
			// 現行キー
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.design_last_used_times");
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.design_count");
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.variation_count");
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.avatar_count");

			// 過去バージョンで残っている可能性のある旧キー
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.design_use_count");
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.avatar_use_count");
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.nail_shape_count");
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.additional_material_count");
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.additional_object_count");
			EditorPrefs.DeleteKey("world.anlabo.mdnailtool.option_count");

			AAOProcessor.ResetDiagnostic();
		}
	}
}

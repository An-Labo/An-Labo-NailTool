using System;
using System.Collections.Generic;
using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity
{
	/// <summary>
	/// 追加オブジェクトセットの定義。
	/// 旧形式（GUIDリスト）と新形式（指制限・指別GUID）の両方を表現する。
	/// </summary>
	[JsonObject]
	public class AdditionalObjectEntry
	{
		/// <summary>
		/// このオブジェクトセットを使用可能な指インデックス (0-19)。
		/// null = 全指で使用可能（旧互換・省略時デフォルト）。
		/// 0-4=左手, 5-9=右手, 10-14=左足, 15-19=右足
		/// </summary>
		[JsonProperty("allowedFingers")]
		public List<int>? AllowedFingers { get; set; }

		/// <summary>
		/// 指別GUIDマッピング。キーは指インデックス文字列 ("0", "5" 等) または "default"。
		/// null の場合は Guids フィールドを使用（旧形式互換）。
		/// </summary>
		[JsonProperty("fingers")]
		public Dictionary<string, List<string>>? Fingers { get; set; }

		/// <summary>
		/// 全指共通GUIDリスト。
		/// Fingers が null の場合にこちらが使用される。
		/// 旧形式からの変換時もここに格納される。
		/// </summary>
		[JsonProperty("guids")]
		public List<string>? Guids { get; set; }

		/// <summary>
		/// 指定指インデックスでこのオブジェクトセットが使用可能か判定する。
		/// </summary>
		public bool IsAllowedForFinger(int fingerIndex)
		{
			if (AllowedFingers == null) return true;
			return AllowedFingers.Contains(fingerIndex);
		}

		/// <summary>
		/// 指定指インデックスのGUIDリストを解決する。
		/// Fingers[index] → Fingers["default"] → Guids → 空 の順で解決。
		/// </summary>
		public IReadOnlyList<string> ResolveGuidsForFinger(int fingerIndex)
		{
			if (Fingers != null)
			{
				if (Fingers.TryGetValue(fingerIndex.ToString(), out List<string>? fingerGuids))
					return fingerGuids;
				if (Fingers.TryGetValue("default", out List<string>? defaultGuids))
					return defaultGuids;
			}
			if (Guids != null) return Guids;
			return Array.Empty<string>();
		}

		/// <summary>
		/// 旧形式 (string[]) から変換するファクトリメソッド。
		/// </summary>
		public static AdditionalObjectEntry FromLegacyGuids(List<string> guids)
		{
			return new AdditionalObjectEntry { Guids = guids };
		}
	}
}

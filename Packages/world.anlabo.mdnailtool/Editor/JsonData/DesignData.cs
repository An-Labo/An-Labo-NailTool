using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.JsonData {
	[JsonObject("design")]
	public class DesignData {

		[JsonProperty("type")]
		public JsonType Type { get; set; } = JsonType.None;

		[JsonProperty("legacyDesign")]
		public LegacyDesignData? Legacy { get; set; }

		public string ToJson() {
			JsonSerializerSettings setting = new() {
				Converters = new List<JsonConverter> {
					new StringEnumConverter()
				},
				Formatting = Formatting.Indented
			};
			return JsonConvert.SerializeObject(this, setting);
		}
		
		public static DesignData ToObject(string json) {
			return JsonConvert.DeserializeObject<DesignData>(json) ?? new DesignData();
		}
		
		public enum JsonType {
			None,
			Legacy,
		}
		
	}
}
using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("nailColorVariation")]
	public class NailColorVariation {
		[JsonRequired]
		[JsonProperty("colorName")]
		public string ColorName { get; set; } = null!;

	}
}
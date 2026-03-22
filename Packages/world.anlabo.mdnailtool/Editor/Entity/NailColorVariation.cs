using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("nailColorVariation")]
	public class NailColorVariation {
		[JsonRequired]
		[JsonProperty("colorName")]
		public string ColorName { get; set; } = null!;

		[JsonProperty("tagColor")]
		public string[]? TagColor { get; set; }

		[JsonProperty("dominantColors")]
		public DominantColor[]? DominantColors { get; set; }
	}

	[JsonObject("dominantColor")]
	public class DominantColor {
		[JsonProperty("hex")]
		public string Hex { get; set; } = null!;

		[JsonProperty("pct")]
		public float Pct { get; set; }
	}
}
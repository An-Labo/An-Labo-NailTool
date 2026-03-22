using System.Collections.Generic;
using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("nailDesign")]
	public class NailDesign {
		[JsonRequired]
		[JsonProperty("id")]
		public int Id { get; set; } = -1;

		[JsonRequired]
		[JsonProperty("designName")]
		public string DesignName { get; set; } = null!;

		[JsonProperty("thumbnailGUID")]
		public string? ThumbnailGUID { get; set; }

		[JsonProperty("tagColor")]
		public string[]? TagColor { get; set; }

		[JsonProperty("tag")]
		public string[]? Tag { get; set; }

		[JsonProperty("subTags")]
		public string[]? SubTags { get; set; }

		[JsonProperty("url")]
		public string? Url { get; set; }

		[JsonProperty("parentVariant")]
		public string? ParentVariant { get; set; }

		[JsonProperty("displayNames")]
		public IReadOnlyDictionary<string, string>? DisplayNames { get; set; }

		[JsonProperty("materialVariation")]
		public IReadOnlyDictionary<string, NailMaterialVariation>? MaterialVariation { get; set; }

		[JsonRequired]
		[JsonProperty("colorVariation")]
		public IReadOnlyDictionary<string, NailColorVariation> ColorVariation { get; set; } = null!;

		[JsonProperty("dominantColors")]
		public DominantColor[]? DominantColors { get; set; }

		[JsonProperty("additionalMaterialGUIDs")]
		public string[]? AdditionalMaterialGUIDs { get; set; }

		[JsonProperty("additionalObjectGUIDs")]
		public IReadOnlyDictionary<string, string[]>? AdditionalObjectGUIDs { get; set; }

		public NailColorVariation? FindVariationByName(string? variationName) {
			if (variationName == null) return null;
			return this.ColorVariation!.GetValueOrDefault(variationName, null);
		}
	}
}
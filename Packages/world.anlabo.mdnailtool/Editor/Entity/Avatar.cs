using System.Collections.Generic;
using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("avatar")]
	public class Avatar {
		[JsonRequired]
		[JsonProperty("avatarName")]
		public string AvatarName { get; set; } = null!;
		
		[JsonProperty("avatarUrl")]
		public string? Url { get; set; }

		[JsonProperty("displayNames")]
		public IReadOnlyDictionary<string, string>? DisplayNames { get; set; }
		
		[JsonRequired]
		[JsonProperty("avatarVariations")]
		public IReadOnlyDictionary<string, AvatarVariation> AvatarVariations { get; set; } = null!;

		[JsonProperty("blendShapeVariants")]
		public AvatarBlendShapeVariant[]? BlendShapeVariants { get; set; }

		public AvatarVariation? FindAvatarVariation(string? name) {
			if (name == null) return null;
			return this.AvatarVariations!.GetValueOrDefault(name, null);
		}
	}
}
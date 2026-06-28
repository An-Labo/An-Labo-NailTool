using System.Collections.Generic;
using System.Linq;
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
		[JsonProperty("reading")]
		public string Reading { get; set; } = null!;

		[JsonRequired]
		[JsonProperty("avatarVariations")]
		public IReadOnlyDictionary<string, AvatarVariation> AvatarVariations { get; set; } = null!;

		[JsonProperty("supportedVersion")]
		public string? SupportedVersion { get; set; }

		[JsonProperty("blendShapeVariants")]
		public AvatarBlendShapeVariant[]? BlendShapeVariants { get; set; }

		public string GetDisplayName(string? language) {
			if (language == "ja" && this.DisplayNames != null && this.DisplayNames.TryGetValue("ja", out string? ja) && !string.IsNullOrEmpty(ja)) {
				return ja;
			}

			return this.AvatarName;
		}

		public string[] GetSearchNames() {
			if (this.DisplayNames == null || this.DisplayNames.Count == 0) return new[] { this.AvatarName };
			return this.DisplayNames.Values
				.Where(name => !string.IsNullOrEmpty(name))
				.Prepend(this.AvatarName)
				.Distinct()
				.ToArray();
		}

		public AvatarVariation? FindAvatarVariation(string? name) {
			if (name == null) return null;
			return this.AvatarVariations!.GetValueOrDefault(name, null);
		}
	}
}
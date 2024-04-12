#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("avatarVariation")]
	public class AvatarVariation {
		[JsonRequired]
		[JsonProperty("variationName")]
		public string VariationName { get; set; } = null!;

		[JsonRequired]
		[JsonProperty("prefabGUID")]
		public string PrefabGUID { get; set; } = null!;

		[JsonProperty("displayNames")]
		public IReadOnlyDictionary<string, string>? DisplayNames { get; set; }

		[JsonProperty("avatarPrefabs")]
		public AvatarPrefab[]? AvatarPrefabs { get; set; }

		[JsonProperty("avatarFbxs")]
		public AvatarFbx[]? AvatarFbxs { get; set; }
	}
}
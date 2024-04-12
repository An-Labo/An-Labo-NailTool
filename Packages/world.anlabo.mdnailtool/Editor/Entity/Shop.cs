using System.Collections.Generic;
using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("shop")]
	public class Shop {
		[JsonRequired]
		[JsonProperty("shopName")]
		public string ShopName { get; set; } = null!;

		[JsonProperty("shopUrl")]
		public string? shopUrl { get; set; }

		[JsonProperty("displayNames")]
		public IReadOnlyDictionary<string, string>? DisplayNames { get; set; }

		[JsonRequired]
		[JsonProperty("avatars")]
		public IReadOnlyDictionary<string, Avatar> Avatars { get; set; } = null!;

		public Avatar? FindAvatarByName(string? avatarName) {
			if (avatarName == null) return null;
			return this.Avatars!.GetValueOrDefault(avatarName, null);
		}
	}
}
#nullable enable
using Newtonsoft.Json;

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject]
	public class AvatarBlendShapeVariant {
		[JsonRequired]
		[JsonProperty("name")]
		public string Name { get; set; } = null!;

		[JsonRequired]
		[JsonProperty("nailPrefabGUID")]
		public string NailPrefabGUID { get; set; } = null!;

		[JsonProperty("syncSourceSmrName")]
		public string? SyncSourceSmrName { get; set; }
	}
}

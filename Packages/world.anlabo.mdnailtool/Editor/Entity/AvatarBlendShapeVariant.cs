#nullable enable
using Newtonsoft.Json;

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject]
	public class AvatarBlendShapeVariant {
		[JsonRequired]
		[JsonProperty("name")]
		public string Name { get; set; } = null!;

		[JsonProperty("nailPrefabName")]
		public string? NailPrefabName { get; set; }

		[JsonProperty("syncSourceSmrName")]
		public string? SyncSourceSmrName { get; set; }

		[JsonProperty("leftBlendShapeName")]
		public string? LeftBlendShapeName { get; set; }

		[JsonProperty("rightBlendShapeName")]
		public string? RightBlendShapeName { get; set; }
	}
}

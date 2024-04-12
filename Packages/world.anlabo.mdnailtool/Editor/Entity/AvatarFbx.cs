using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("avatarFbx")]
	public class AvatarFbx {
		[JsonProperty("fbxName")]
		public string? FbxName { get; set; }

		[JsonProperty("fbxGUID")]
		public string? FbxGUID { get; set; }

		[JsonProperty("bodyName")]
		public string? BodyName { get; set; }
	}
}
using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("avatarPrefab")]
	public class AvatarPrefab {

		[JsonProperty("prefabName")]
		public string? PrefabName { get; set; }

		[JsonProperty("prefabGUID")]
		public string? PrefabGUID { get; set; }
		
	}
}
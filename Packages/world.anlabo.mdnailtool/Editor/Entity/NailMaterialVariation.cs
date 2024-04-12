using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("nailMaterialVariation")]
	public class NailMaterialVariation {

		[JsonRequired]
		[JsonProperty("materialName")]
		public string MaterialName { get; set; } = null!;

	}
}
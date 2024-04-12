using Newtonsoft.Json;

#nullable enable
namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("nailShape")]
	public class NailShape {
		[JsonRequired]
		[JsonProperty("shapeName")]
		public string ShapeName { get; set; } = null!;

		[JsonRequired]
		[JsonProperty("fbxFolderGUID")]
		public string FbxFolderGUID { get; set; } = null!;

		[JsonProperty("fbxNamePrefix")]
		public string? FbxNamePrefix { get; set; }

		[JsonRequired]
		[JsonProperty("normalMapGUID")]
		public string NormalMapGUID { get; set; } = null!;
	}
}
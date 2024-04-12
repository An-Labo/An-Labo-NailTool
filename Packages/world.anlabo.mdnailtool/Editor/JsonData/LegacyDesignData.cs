using System.Collections.Generic;
using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.JsonData {
	[JsonObject("legacyDesign")]
	public class LegacyDesignData {
		[JsonRequired]
		[JsonProperty("designDirectoryGUID")]
		public string DesignDirectoryGUID { get; set; } = null!;

		[JsonProperty("additionalMaterialGUIDs")]
		public  string[]? AdditionalMaterialGUIDs { get; set; }

		[JsonProperty("additionalObjectGUIDs")]
		public IReadOnlyDictionary<MDNailToolDefines.TargetFinger, string[]>? AdditionalObjectGUIDs { get; set; }
	}
}
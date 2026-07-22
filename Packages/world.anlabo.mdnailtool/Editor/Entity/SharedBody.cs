#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace world.anlabo.mdnailtool.Editor.Entity {
	// shop.json ルートの sharedBodies プール 1 件分. Loader が variation.SharedBodyId から expand する.
	[JsonObject("sharedBody")]
	public class SharedBody {
		[JsonProperty("boneMappingOverride")]
		public IReadOnlyDictionary<string, string>? BoneMappingOverride { get; set; }
		[JsonProperty("nailNodes")]
		public NailPrefabNodeData[]? NailNodes { get; set; }

		// shape 非依存の足ネイル node. name は prefix なし (Loader が各 [Shape] root の children に再注入).
		[JsonProperty("footNailNodes")]
		public NailPrefabNodeData[]? FootNailNodes { get; set; }
	}
}

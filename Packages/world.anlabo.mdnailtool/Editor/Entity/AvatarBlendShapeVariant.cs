#nullable enable
using Newtonsoft.Json;

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject]
	public class AvatarBlendShapeVariant {
		[JsonRequired]
		[JsonProperty("name")]
		public string Name { get; set; } = null!;

		// NailNodes 経路に統一. NailPrefabGUID は legacy 後方互換 (旧 JSON で nailNodes 不在時のみ参照).
		[JsonProperty("nailPrefabGUID")]
		public string? NailPrefabGUID { get; set; }

		[JsonProperty("syncSourceSmrName")]
		public string? SyncSourceSmrName { get; set; }

		[JsonProperty("leftBlendShapeName")]
		public string? LeftBlendShapeName { get; set; }

		[JsonProperty("rightBlendShapeName")]
		public string? RightBlendShapeName { get; set; }

		// Stage9: 物理 prefab を廃止する経路. NailNodes が居れば NailPrefabGUID 経路を skip して BuildFromNodes で復元.
		[JsonProperty("nailNodes")]
		public NailPrefabNodeData[]? NailNodes { get; set; }
	}
}

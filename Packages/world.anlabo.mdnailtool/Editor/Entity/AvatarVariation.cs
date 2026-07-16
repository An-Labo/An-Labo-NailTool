#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("avatarVariation")]
	public class AvatarVariation {
		[JsonRequired]
		[JsonProperty("variationName")]
		public string VariationName { get; set; } = null!;

		// sharedBodyId 経路 / nailNodes 経路 双方で空のことがあるため optional 化.
		[JsonProperty("nailPrefabGUID")]
		public string? NailPrefabGUID { get; set; }

		[JsonProperty("displayNames")]
		public IReadOnlyDictionary<string, string>? DisplayNames { get; set; }

		[JsonProperty("boneMappingOverride")]
		public IReadOnlyDictionary<string, string>? BoneMappingOverride { get; set; }

		[JsonProperty("blendShapeSyncSources")]
		public string[]? BlendShapeSyncSources { get; set; }

		[JsonProperty("blendShapeInitialWeights")]
		public IReadOnlyDictionary<string, float>? BlendShapeInitialWeights { get; set; }

		[JsonProperty("blendShapeVariants")]
		public AvatarBlendShapeVariant[]? BlendShapeVariants { get; set; }

		[JsonProperty("avatarPrefabs")]
		public AvatarPrefab[] AvatarPrefabs { get; set; } = Array.Empty<AvatarPrefab>();

		[JsonProperty("avatarFbxs")]
		public AvatarFbx[] AvatarFbxs { get; set; } = Array.Empty<AvatarFbx>();

		// 全 shape の root を concat した配列. 各 root の name は `[Shape]xxx` 形式. Loader 側で prefix filter で shape 別に取り出す.
		// 共有素体経由の variation でも DBShop loader が SharedBodyId を resolve して注入するため、consumer は常にこれを見れば足りる.
		[JsonProperty("nailNodes")]
		public NailPrefabNodeData[]? NailNodes { get; set; }

		// 共有素体プール参照. set されてる variation の nailNodes / footNailNodes は json 上空で、Loader が sharedBodies pool から expand する.
		[JsonProperty("sharedBodyId")]
		public string? SharedBodyId { get; set; }

		// sharedBodyId で共有した素体を、この variation 用に XYZ 倍率補正する. 未指定は [1,1,1].
		[JsonProperty("sharedBodyScale")]
		public float[]? SharedBodyScale { get; set; }

		// shape 非依存の足ネイル node. name は prefix なし (例: `FootR.Thumb`). Loader 側で各 [Shape] root の children に再注入される.
		[JsonProperty("footNailNodes")]
		public NailPrefabNodeData[]? FootNailNodes { get; set; }
	}
}
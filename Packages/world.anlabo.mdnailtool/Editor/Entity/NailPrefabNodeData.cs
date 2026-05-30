#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace world.anlabo.mdnailtool.Editor.Entity {
	// nail prefab 内の 1ノード分のデータ。mesh は avatarパッケージ側の GUID を参照する
	[JsonObject]
	public class NailPrefabNodeData {
		[JsonProperty("name")]
		public string Name { get; set; } = null!;

		// [x,y,z]
		[JsonProperty("localPosition")]
		public float[]? LocalPosition { get; set; }

		// [x,y,z,w] quaternion
		[JsonProperty("localRotation")]
		public float[]? LocalRotation { get; set; }

		// [x,y,z]
		[JsonProperty("localScale")]
		public float[]? LocalScale { get; set; }

		// SkinnedMeshRenderer が参照する mesh GUID (avatar package 側)
		[JsonProperty("meshGuid")]
		public string? MeshGuid { get; set; }

		// SkinnedMeshRenderer の rootBone の名前 (階層名)
		[JsonProperty("rootBoneName")]
		public string? RootBoneName { get; set; }

		// ブレンドシェイプ名 -> weight (非ゼロのみ)
		[JsonProperty("blendShapeWeights")]
		public Dictionary<string, float>? BlendShapeWeights { get; set; }

		// 子ノード
		[JsonProperty("children")]
		public NailPrefabNodeData[]? Children { get; set; }
	}
}

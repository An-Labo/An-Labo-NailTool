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

		// SkinnedMeshRenderer.localBounds.center [x,y,z]
		[JsonProperty("boundsCenter")]
		public float[]? BoundsCenter { get; set; }

		// SkinnedMeshRenderer.localBounds.extents [x,y,z]
		[JsonProperty("boundsExtent")]
		public float[]? BoundsExtent { get; set; }

		// 子ノード
		[JsonProperty("children")]
		public NailPrefabNodeData[]? Children { get; set; }

		// "smr" / "mr" / null (Transform のみ). null の場合 children だけ.
		[JsonProperty("rendererType")]
		public string? RendererType { get; set; }

		// 内蔵 mesh (Unity DefaultResources 等) の fileID. MeshGuid と組合せて Resources.GetBuiltinResource で復元.
		[JsonProperty("meshFileId")]
		public long? MeshFileId { get; set; }

		// MeshRenderer の sharedMaterials GUID 配列 (MR 用. SMR は ApplyMaterial で動的差替のため不要).
		[JsonProperty("materialGuids")]
		public string[]? MaterialGuids { get; set; }
	}
}

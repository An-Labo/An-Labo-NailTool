#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace world.anlabo.mdnailtool.Editor.Entity {
	// _MainTex は ColorTextures で別管理するため含めない
	[JsonObject]
	public class NailMaterialDelta {
		[JsonProperty("shaderName")]
		public string ShaderName { get; set; } = null!;

		// テクスチャプロパティ名 -> AssetDatabase GUID (_MainTex を除く固定テクスチャのみ)
		[JsonProperty("textures")]
		public Dictionary<string, string>? Textures { get; set; }

		[JsonProperty("floats")]
		public Dictionary<string, float>? Floats { get; set; }

		// Color プロパティ名 -> [r,g,b,a] (0~1 float)
		[JsonProperty("colors")]
		public Dictionary<string, float[]>? Colors { get; set; }

		[JsonProperty("vectors")]
		public Dictionary<string, float[]>? Vectors { get; set; }

		[JsonProperty("materialName")]
		public string? MaterialName { get; set; }
	}
}

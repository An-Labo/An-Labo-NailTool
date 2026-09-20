using System.Collections.Generic;
using System;
using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity {
	[JsonObject("nailDesign")]
	public class NailDesign {
		[JsonRequired]
		[JsonProperty("id")]
		public int Id { get; set; } = -1;

		// id is a stable lookup key. sortOrder is only needed when historical
		// release order cannot be represented by that key (for example, after
		// repairing a duplicate legacy id).
		[JsonProperty("sortOrder")]
		public int? SortOrder { get; set; }

		[JsonIgnore]
		public int EffectiveSortOrder => this.SortOrder ?? this.Id;

		[JsonRequired]
		[JsonProperty("designName")]
		public string DesignName { get; set; } = null!;

		[JsonProperty("thumbnailGUID")]
		public string? ThumbnailGUID { get; set; }

		[JsonProperty("tagColor")]
		public string[]? TagColor { get; set; }

		[JsonProperty("tag")]
		public string[]? Tag { get; set; }

		[JsonProperty("subTags")]
		public string[]? SubTags { get; set; }

		[JsonProperty("url")]
		public string? Url { get; set; }

		[JsonProperty("parentVariant")]
		public string? ParentVariant { get; set; }

		[JsonProperty("displayNames")]
		public IReadOnlyDictionary<string, string>? DisplayNames { get; set; }

		[JsonProperty("materialVariation")]
		public IReadOnlyDictionary<string, NailMaterialVariation>? MaterialVariation { get; set; }

		[JsonRequired]
		[JsonProperty("colorVariation")]
		public IReadOnlyDictionary<string, NailColorVariation> ColorVariation { get; set; } = null!;

		[JsonProperty("dominantColors")]
		public DominantColor[]? DominantColors { get; set; }

		[JsonProperty("additionalMaterialGUIDs")]
		public string[]? AdditionalMaterialGUIDs { get; set; }

		// shape -> additional material GUIDs or additionalAssets registry names.
		// A matching shape entry (including an empty array) takes precedence over
		// the legacy design-wide additionalMaterialGUIDs list.
		[JsonProperty("additionalMaterialGUIDsByShape")]
		public IReadOnlyDictionary<string, string[]>? AdditionalMaterialGUIDsByShape { get; set; }

		[JsonProperty("additionalObjectGUIDs")]
		public IReadOnlyDictionary<string, string[]>? AdditionalObjectGUIDs { get; set; }

		public IReadOnlyList<string> GetAdditionalMaterialReferences(string nailShapeName) {
			if (this.AdditionalMaterialGUIDsByShape != null) {
				var matchedKeys = new List<string>();
				foreach (KeyValuePair<string, string[]> entry in this.AdditionalMaterialGUIDsByShape) {
					if (string.Equals(entry.Key, nailShapeName, StringComparison.OrdinalIgnoreCase)) {
						matchedKeys.Add(entry.Key);
					}
				}
				if (matchedKeys.Count > 1) {
					throw new InvalidOperationException(
						$"Duplicate additionalMaterialGUIDsByShape keys (case-insensitive): {string.Join(", ", matchedKeys)}");
				}
				if (matchedKeys.Count == 1) {
					return this.AdditionalMaterialGUIDsByShape[matchedKeys[0]] ?? Array.Empty<string>();
				}
			}

			return this.AdditionalMaterialGUIDs ?? Array.Empty<string>();
		}

		// materialName -> NailMaterialDelta。非 null なら zip 展開不要でマテリアル再構築可能
		[JsonProperty("materialData")]
		public IReadOnlyDictionary<string, NailMaterialDelta>? MaterialData { get; set; }

		// shape -> materialName -> colorVariationName -> _MainTex GUID
		[JsonProperty("colorTextures")]
		public IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>? ColorTextures { get; set; }

		// design 単位のデフォルト MatCap テクスチャ GUID. material 個別の _MatCapTex が無ければこれを当てる.
		[JsonProperty("_matCapDefault")]
		public string? MatCapDefault { get; set; }

		public NailColorVariation? FindVariationByName(string? variationName) {
			if (variationName == null) return null;
			return this.ColorVariation!.GetValueOrDefault(variationName, null);
		}
	}
}

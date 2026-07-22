using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using world.anlabo.mdnailtool.Editor.Entity;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Model {
	public class DBShop : DBBase<Shop> {

		public DBShop() : base(MDNailToolDefines.DB_SHOP_FILE_PATH) {}

		public Shop? FindShopByName(string? name) {
			if (name == null) return null;
			return this._data!.GetValueOrDefault(name, null);
		}

		// shop.json は root が `{ "sharedBodies": {...}, "<shopName>": <Shop>, ... }` 形式. sharedBodies を分離し pool を variation に展開する.
		protected override Dictionary<string, Shop>? DeserializeRoot(string jsonText) {
			JObject? root = JsonConvert.DeserializeObject<JObject>(jsonText);
			if (root == null) return null;

			Dictionary<string, SharedBody> pool = new();
			if (root.TryGetValue("sharedBodies", out JToken? sbToken) && sbToken is JObject sbObj) {
				foreach (var prop in sbObj.Properties()) {
					SharedBody? sb = prop.Value.ToObject<SharedBody>();
					if (sb != null) pool[prop.Name] = sb;
				}
				root.Remove("sharedBodies");
			}

			Dictionary<string, Shop>? shops = root.ToObject<Dictionary<string, Shop>>();
			if (shops == null) return null;

			ValidateAvatarReadings(shops);
			ExpandSharedBodiesIntoVariations(shops, pool);
			MergeFootNailNodesIntoShapeRoots(shops);
			return shops;
		}

		private static void ValidateAvatarReadings(Dictionary<string, Shop> shops) {
			foreach (var shopPair in shops) {
				Shop shop = shopPair.Value;
				if (shop.Avatars == null) continue;
				foreach (var avatarPair in shop.Avatars) {
					Avatar avatar = avatarPair.Value;
					if (avatar == null) continue;
					if (!string.IsNullOrWhiteSpace(avatar.Reading)) continue;
					string shopName = string.IsNullOrEmpty(shop.ShopName) ? shopPair.Key : shop.ShopName;
					string avatarName = string.IsNullOrEmpty(avatar.AvatarName) ? avatarPair.Key : avatar.AvatarName;
					throw new NailToolResourceException("DB", $"shop.json avatar reading is required: {shopName}/{avatarName}");
				}
			}
		}

		private static void ExpandSharedBodiesIntoVariations(Dictionary<string, Shop> shops, Dictionary<string, SharedBody> pool) {
			if (pool.Count == 0) return;
			foreach (Shop shop in shops.Values) {
				if (shop.Avatars == null) continue;
				foreach (Avatar avatar in shop.Avatars.Values) {
					if (avatar.AvatarVariations == null) continue;
					foreach (AvatarVariation variation in avatar.AvatarVariations.Values) {
						if (variation == null || string.IsNullOrEmpty(variation.SharedBodyId)) continue;
						if (!pool.TryGetValue(variation.SharedBodyId!, out SharedBody? sb) || sb == null) continue;
						variation.BoneMappingOverride = MergeBoneMappingOverrides(sb.BoneMappingOverride, variation.BoneMappingOverride);
						variation.NailNodes = CloneNodes(sb.NailNodes);
						variation.FootNailNodes = CloneNodes(sb.FootNailNodes);
						ApplySharedBodyScale(variation.NailNodes, variation.SharedBodyScale);
						ApplySharedBodyScaleAsChildren(variation.FootNailNodes, variation.SharedBodyScale);
						ApplySharedBodyScale(variation.BlendShapeVariants, variation.SharedBodyScale);
					}
				}
			}
		}


		private static IReadOnlyDictionary<string, string>? MergeBoneMappingOverrides(
			IReadOnlyDictionary<string, string>? shared,
			IReadOnlyDictionary<string, string>? variation) {
			if (shared == null || shared.Count == 0) return variation;
			Dictionary<string, string> merged = new();
			foreach (KeyValuePair<string, string> pair in shared) merged[pair.Key] = pair.Value;
			if (variation != null) {
				foreach (KeyValuePair<string, string> pair in variation) merged[pair.Key] = pair.Value;
			}
			return merged;
		}
		private static void ApplySharedBodyScale(NailPrefabNodeData[]? nodes, float[]? scale) {
			if (nodes == null || nodes.Length == 0 || !HasSharedBodyScale(scale)) return;
			for (int i = 0; i < nodes.Length; i++) ApplyRootScale(nodes[i], scale!);
		}

		private static void ApplySharedBodyScaleAsChildren(NailPrefabNodeData[]? nodes, float[]? scale) {
			if (nodes == null || nodes.Length == 0 || !HasSharedBodyScale(scale)) return;
			for (int i = 0; i < nodes.Length; i++) ApplyScaleAsChild(nodes[i], scale!);
		}


		private static void ApplySharedBodyScale(AvatarBlendShapeVariant[]? variants, float[]? scale) {
			if (variants == null || variants.Length == 0 || !HasSharedBodyScale(scale)) return;
			foreach (AvatarBlendShapeVariant variant in variants) ApplySharedBodyScale(variant.NailNodes, scale);
		}

		private static bool HasSharedBodyScale(float[]? scale) {
			if (scale == null || scale.Length < 3) return false;
			return !IsNearlyOne(scale[0]) || !IsNearlyOne(scale[1]) || !IsNearlyOne(scale[2]);
		}

		private static bool IsNearlyOne(float value) {
			return value > 0.9999f && value < 1.0001f;
		}

		private static void ApplyRootScale(NailPrefabNodeData node, float[] scale) {
			if (node.Children == null || node.Children.Length == 0) {
				ApplyScaleToSelf(node, scale);
				return;
			}
			foreach (NailPrefabNodeData child in node.Children) ApplyScaleAsChild(child, scale);
		}

		private static void ApplyScaleAsChild(NailPrefabNodeData node, float[] scale) {
			if (node.LocalPosition != null && node.LocalPosition.Length >= 3) {
				node.LocalPosition = new[] { node.LocalPosition[0] * scale[0], node.LocalPosition[1] * scale[1], node.LocalPosition[2] * scale[2] };
			}
			if (node.Children != null && node.Children.Length > 0) {
				foreach (NailPrefabNodeData child in node.Children) ApplyScaleAsChild(child, scale);
				return;
			}
			ApplyScaleToSelf(node, scale);
		}

		private static void ApplyScaleToSelf(NailPrefabNodeData node, float[] scale) {
			float x = scale[0];
			float y = scale[1];
			float z = scale[2];
			if (node.LocalScale != null && node.LocalScale.Length >= 3) {
				node.LocalScale = new[] { node.LocalScale[0] * x, node.LocalScale[1] * y, node.LocalScale[2] * z };
			} else {
				node.LocalScale = new[] { x, y, z };
			}
		}
		// footNailNodes (prefix なし) を各 [Shape] root の children に prefix 付きで再注入する. consumer は従来通り NailNodes だけで完結する.
		private static void MergeFootNailNodesIntoShapeRoots(Dictionary<string, Shop> shops) {
			foreach (Shop shop in shops.Values) {
				if (shop.Avatars == null) continue;
				foreach (Avatar avatar in shop.Avatars.Values) {
					if (avatar.AvatarVariations == null) continue;
					foreach (AvatarVariation variation in avatar.AvatarVariations.Values) {
						if (variation == null) continue;
						MergeFootIntoVariation(variation);
					}
				}
			}
		}

		private static void MergeFootIntoVariation(AvatarVariation variation) {
			NailPrefabNodeData[]? foot = variation.FootNailNodes;
			NailPrefabNodeData[]? nailNodes = variation.NailNodes;
			if (foot == null || foot.Length == 0 || nailNodes == null || nailNodes.Length == 0) return;

			foreach (NailPrefabNodeData shapeRoot in nailNodes) {
				string shapePrefix = ExtractShapePrefix(shapeRoot.Name);
				if (string.IsNullOrEmpty(shapePrefix)) continue;
				NailPrefabNodeData[] prefixedFoot = new NailPrefabNodeData[foot.Length];
				for (int i = 0; i < foot.Length; i++) {
					prefixedFoot[i] = ClonePrefixed(foot[i], shapePrefix);
				}
				shapeRoot.Children = ConcatChildren(shapeRoot.Children, prefixedFoot);
			}
		}

		private static string ExtractShapePrefix(string? name) {
			if (string.IsNullOrEmpty(name) || name![0] != '[') return string.Empty;
			int end = name.IndexOf(']');
			return end < 0 ? string.Empty : name.Substring(0, end + 1);
		}

		private static NailPrefabNodeData ClonePrefixed(NailPrefabNodeData src, string shapePrefix) {
			NailPrefabNodeData copy = CloneNode(src);
			copy.Name = shapePrefix + (src.Name ?? string.Empty);
			if (src.Children != null) {
				NailPrefabNodeData[] prefixedChildren = new NailPrefabNodeData[src.Children.Length];
				for (int i = 0; i < src.Children.Length; i++) {
					prefixedChildren[i] = ClonePrefixed(src.Children[i], shapePrefix);
				}
				copy.Children = prefixedChildren;
			}
			return copy;
		}

		private static NailPrefabNodeData[]? CloneNodes(NailPrefabNodeData[]? src) {
			if (src == null) return null;
			NailPrefabNodeData[] copy = new NailPrefabNodeData[src.Length];
			for (int i = 0; i < src.Length; i++) copy[i] = CloneNode(src[i]);
			return copy;
		}

		private static NailPrefabNodeData CloneNode(NailPrefabNodeData src) {
			NailPrefabNodeData copy = new() {
				Name = src.Name,
				LocalPosition = src.LocalPosition,
				LocalRotation = src.LocalRotation,
				LocalScale = src.LocalScale,
				MeshGuid = src.MeshGuid,
				RootBoneName = src.RootBoneName,
				BlendShapeWeights = src.BlendShapeWeights,
				BoundsCenter = src.BoundsCenter,
				BoundsExtent = src.BoundsExtent,
				RendererType = src.RendererType,
				MeshFileId = src.MeshFileId,
				MaterialGuids = src.MaterialGuids,
			};
			if (src.Children != null) {
				NailPrefabNodeData[] childCopy = new NailPrefabNodeData[src.Children.Length];
				for (int i = 0; i < src.Children.Length; i++) childCopy[i] = CloneNode(src.Children[i]);
				copy.Children = childCopy;
			}
			return copy;
		}

		private static NailPrefabNodeData[] ConcatChildren(NailPrefabNodeData[]? existing, NailPrefabNodeData[] add) {
			if (existing == null || existing.Length == 0) return add;
			NailPrefabNodeData[] combined = new NailPrefabNodeData[existing.Length + add.Length];
			existing.CopyTo(combined, 0);
			add.CopyTo(combined, existing.Length);
			return combined;
		}
	}
}





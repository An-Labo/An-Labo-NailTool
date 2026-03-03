using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using Avatar = world.anlabo.mdnailtool.Editor.Entity.Avatar;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public class AvatarMatching {
		private readonly GameObject _avatarObject;
		private readonly VRCAvatarDescriptor _avatar;

		public AvatarMatching(VRCAvatarDescriptor avatar) {
			this._avatarObject = avatar.gameObject;
			this._avatar = avatar;
		}

		public (Shop, Avatar, AvatarVariation)? Match() {
			using DBShop dbShop = new();

			if (PrefabUtility.IsAnyPrefabInstanceRoot(this._avatarObject)) {
				// プレハブの場合
				List<GameObject> avatarPrefabs = new() { this._avatarObject };

				{
					// プレハブツリーを取得
					GameObject current = this._avatarObject;
					while (true) {
						GameObject parent = PrefabUtility.GetCorrespondingObjectFromSource(current);
						if (parent == null) break;
						if (parent == current) break;
						avatarPrefabs.Add(parent);
						current = parent;
					}
				}

				avatarPrefabs.Reverse();

				IEnumerable<(string? prefabName, string? prefabGuid, ShopAndAvatarAndVariation variation)> prefabs = dbShop.collection
					.SelectMany(shop => shop.Avatars.Select(pair => new ShopAndAvatar { Shop = shop, Avatar = pair.Value }))
					.SelectMany(avatar => avatar.Avatar.AvatarVariations.Select(pair => new ShopAndAvatarAndVariation { Shop = avatar.Shop, Avatar = avatar.Avatar, Variation = pair.Value }))
					.SelectMany(variation => variation.Variation.AvatarPrefabs.Select(prefab => (prefab.PrefabName, prefab.PrefabGUID, variation)))
					.ToArray();

				foreach ((string? _, string? targetGuid, ShopAndAvatarAndVariation variation) in prefabs) {
					if (string.IsNullOrEmpty(targetGuid)) continue;
					if (avatarPrefabs
					    .Select(avatarPrefab => AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(avatarPrefab)).ToString())
					    .Any(prefabGuid => prefabGuid == targetGuid)) {
						return (variation.Shop, variation.Avatar, variation.Variation);
					}
				}

				foreach ((string? targetName, string? _, ShopAndAvatarAndVariation variation) in prefabs) {
					if (string.IsNullOrEmpty(targetName)) continue;
					if (avatarPrefabs
					    .Select(avatarPrefab => avatarPrefab.name)
					    .Any(prefabName => prefabName.Contains(targetName))) {
						return (variation.Shop, variation.Avatar, variation.Variation);
					}
				}

			}

			// FBXベースマッチング（全SkinnedMeshRendererスキャン）

			// DB内のFBXデータを事前取得
			var fbxEntries = dbShop.collection
				.SelectMany(shop => shop.Avatars.Select(pair => new ShopAndAvatar { Shop = shop, Avatar = pair.Value }))
				.SelectMany(avatar => avatar.Avatar.AvatarVariations.Select(pair => new ShopAndAvatarAndVariation { Shop = avatar.Shop, Avatar = avatar.Avatar, Variation = pair.Value }))
				.SelectMany(variation => variation.Variation.AvatarFbxs.Select(fbx => (fbx.FbxName, fbx.FbxGUID, variation)))
				.ToArray();

			// 全SkinnedMeshRendererをスキャンしてDBマッチ
			SkinnedMeshRenderer? visemeSmr = this._avatar.VisemeSkinnedMesh;
			var matchResults = new List<(SkinnedMeshRenderer smr, ShopAndAvatarAndVariation variation)>();

			foreach (SkinnedMeshRenderer smr in this._avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
				Mesh? mesh = smr.sharedMesh;
				if (mesh == null) continue;
				string smrFbxPath = AssetDatabase.GetAssetPath(mesh);
				if (string.IsNullOrEmpty(smrFbxPath)) continue;

				ShopAndAvatarAndVariation? matched = MatchFbxAgainstDB(smrFbxPath, fbxEntries);
				if (matched != null) {
					matchResults.Add((smr, matched.Value));
				}
			}

			if (matchResults.Count == 0) return null;

			// アバター単位でグルーピング（Shop+Avatar+Variationで同一判定）
			var avatarGroups = matchResults
				.GroupBy(m => (m.variation.Shop.ShopName, m.variation.Avatar.AvatarName, m.variation.Variation.VariationName))
				.ToList();

			// 1種類だけ → 確定
			if (avatarGroups.Count == 1) {
				var first = avatarGroups[0].First();
				return (first.variation.Shop, first.variation.Avatar, first.variation.Variation);
			}

			// 複数種類 → VisemeSkinnedMesh(顔)のSmRを除外して体アバターを探す
			var bodyCandidates = matchResults.Where(m => m.smr != visemeSmr).ToList();
			if (bodyCandidates.Count == 0) {
				// 全部顔SmRだった（通常ありえないが安全策）→ 最初のマッチを返す
				var first = matchResults[0];
				return (first.variation.Shop, first.variation.Avatar, first.variation.Variation);
			}

			// 体候補をアバター単位で再グルーピング
			var bodyAvatarGroups = bodyCandidates
				.GroupBy(m => (m.variation.Shop.ShopName, m.variation.Avatar.AvatarName, m.variation.Variation.VariationName))
				.ToList();

			// 体アバターが1種類 → 確定
			if (bodyAvatarGroups.Count == 1) {
				var first = bodyAvatarGroups[0].First();
				return (first.variation.Shop, first.variation.Avatar, first.variation.Variation);
			}

			// まだ複数 → 優先順位で選ぶ（表示中 > EditorOnly > 非表示）
			var best = bodyCandidates
				.OrderBy(m => GetSmrPriority(m.smr))
				.First();
			return (best.variation.Shop, best.variation.Avatar, best.variation.Variation);
		}

		/// <summary>
		/// FBXパスをDB内のエントリと照合し、マッチしたアバターバリエーションを返す。
		/// GUID一致を優先し、次に名前一致を試す。
		/// </summary>
		private static ShopAndAvatarAndVariation? MatchFbxAgainstDB(
			string fbxPath,
			IEnumerable<(string? FbxName, string? FbxGuid, ShopAndAvatarAndVariation variation)> fbxEntries) {

			string fbxGuid = AssetDatabase.GUIDFromAssetPath(fbxPath).ToString();
			string fbxName = Path.GetFileName(fbxPath);

			// GUID一致（優先）
			foreach (var (_, targetGuid, variation) in fbxEntries) {
				if (string.IsNullOrEmpty(targetGuid)) continue;
				if (fbxGuid == targetGuid) return variation;
			}

			// 名前一致
			foreach (var (targetName, _, variation) in fbxEntries) {
				if (string.IsNullOrEmpty(targetName)) continue;
				if (fbxName.Contains(targetName)) return variation;
			}

			return null;
		}

		/// <summary>
		/// SkinnedMeshRendererの優先順位を返す。
		/// 表示中かつ非EditorOnly(0) > EditorOnly(1) > 非表示(2)
		/// </summary>
		private static int GetSmrPriority(SkinnedMeshRenderer smr) {
			bool active = smr.gameObject.activeInHierarchy && smr.enabled;
			bool editorOnly = IsEditorOnly(smr.transform);
			if (active && !editorOnly) return 0; // 最優先: 表示中 & 非EditorOnly
			if (editorOnly) return 1;             // 次点: EditorOnly（誤設定の可能性）
			return 2;                             // 最後: 非表示
		}

		/// <summary>
		/// Transform自身または祖先にEditorOnlyタグがあるか判定する。
		/// </summary>
		private static bool IsEditorOnly(Transform transform) {
			Transform? current = transform;
			while (current != null) {
				if (current.CompareTag("EditorOnly")) return true;
				current = current.parent;
			}
			return false;
		}

		private struct ShopAndAvatar {
			public Shop Shop;
			public Avatar Avatar;
		}

		private struct ShopAndAvatarAndVariation {
			public Shop Shop;
			public Avatar Avatar;
			public AvatarVariation Variation;

		}
	}
}

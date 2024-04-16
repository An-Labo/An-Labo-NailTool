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

			// FBXベースマッチング

			if (this._avatar.VisemeSkinnedMesh == null) return null;
			SkinnedMeshRenderer faceSkinnedMeshRenderer = this._avatar.VisemeSkinnedMesh;
			Mesh faceMesh = faceSkinnedMeshRenderer.sharedMesh;
			string fbxPath = AssetDatabase.GetAssetPath(faceMesh);
			string fbxGuid = AssetDatabase.GUIDFromAssetPath(fbxPath).ToString();
			string fbxName = Path.GetFileName(fbxPath);

			IEnumerable<(string? FbxName, string? FbxGuid, ShopAndAvatarAndVariation variation)> fbxs = dbShop.collection
				.SelectMany(shop => shop.Avatars.Select(pair => new ShopAndAvatar { Shop = shop, Avatar = pair.Value }))
				.SelectMany(avatar => avatar.Avatar.AvatarVariations.Select(pair => new ShopAndAvatarAndVariation { Shop = avatar.Shop, Avatar = avatar.Avatar, Variation = pair.Value }))
				.SelectMany(variation => variation.Variation.AvatarFbxs.Select(fbx => (fbx.FbxName, fbx.FbxGUID, variation)))
				.ToArray();
			
			foreach ((string? _, string? targetGuid, ShopAndAvatarAndVariation variation) in fbxs) {
				if (string.IsNullOrEmpty(targetGuid)) continue;
				if (fbxGuid != targetGuid) continue;
				return (variation.Shop, variation.Avatar, variation.Variation);
			}
			
			foreach ((string? targetName, string? _, ShopAndAvatarAndVariation variation) in fbxs) {
				if (string.IsNullOrEmpty(targetName)) continue;
				if (!fbxName.Contains(targetName)) continue;
				return (variation.Shop, variation.Avatar, variation.Variation);
			}

			return null;
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
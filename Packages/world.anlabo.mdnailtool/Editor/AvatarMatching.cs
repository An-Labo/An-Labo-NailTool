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
			if (PrefabUtility.IsAnyPrefabInstanceRoot(this._avatarObject)) {
				// プレハブの場合
				// TODO: 現状プレハブベースのマッチングは行わない(データがないため)
				/*
				Queue<GameObject> prefabQueue = new();
				prefabQueue.Enqueue(this._avatarObject);

				{
					// プレハブツリーを取得
					GameObject current = this._avatarObject;
					while (true) {
						GameObject parent = PrefabUtility.GetCorrespondingObjectFromSource(current);
						if (parent == null) break;
						if (parent == current) break;
						prefabQueue.Enqueue(parent);
						current = parent;
					}
				}

				using DBShop dbShop = new();
				dbShop.collection.SelectMany(shop => shop.Avatars!.Values).SelectMany(avatar => avatar.AvatarVariations);
				HashSet<string> prefabNames;

				while (prefabQueue.Count > 0) {
					GameObject current = prefabQueue.Dequeue();

				}
				*/
			}

			// FBXベースマッチング

			if (this._avatar.VisemeSkinnedMesh == null) return null;
			SkinnedMeshRenderer faceSkinnedMeshRenderer = this._avatar.VisemeSkinnedMesh;
			Mesh faceMesh = faceSkinnedMeshRenderer.sharedMesh;
			string fbxPath = AssetDatabase.GetAssetPath(faceMesh);
			string fbxName = Path.GetFileName(fbxPath);

			using DBShop dbShop = new();

			IEnumerable<(string? FbxName, ShopAndAvatarAndVariation variation)> fbxNames = dbShop.collection
				.SelectMany(shop => shop.Avatars.Select(pair => new ShopAndAvatar { Shop = shop, Avatar = pair.Value }))
				.SelectMany(avatar => avatar.Avatar.AvatarVariations.Select(pair => new ShopAndAvatarAndVariation { Shop = avatar.Shop, Avatar = avatar.Avatar, Variation = pair.Value }))
				.SelectMany(variation => variation.Variation.AvatarFbxs!.Select(fbx => (fbx.FbxName, variation)));
			
			foreach ((string? targetName, ShopAndAvatarAndVariation variation) in fbxNames) {
				if (targetName == null) continue;
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
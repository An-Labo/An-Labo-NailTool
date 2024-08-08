using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using Object = UnityEngine.Object;
using world.anlabo.mdnailtool.Runtime;
using world.anlabo.mdnailtool.Runtime.Extensions;
using Avatar = UnityEngine.Avatar;

#if MD_NAIL_FOR_MA
using nadena.dev.modular_avatar.core;
#endif

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public class NailSetupProcessor {
		
		private VRCAvatarDescriptor Avatar { get; }
		private GameObject NailPrefab { get; set; }
		private (INailProcessor, string, string)[] NailDesignAndVariationNames { get; }
		private string NailShapeName { get; }

		public Mesh?[]? OverrideMesh { get; set; }

		public string AvatarName { get; set; } = null!;
		public bool UseFootNail { get; set; }
		public bool RemoveCurrentNail { get; set; }
		public bool GenerateMaterial { get; set; }
		public bool Backup { get; set; }
		public bool ForModularAvatar { get; set; }

		public NailSetupProcessor(VRCAvatarDescriptor avatar, GameObject nailPrefab, (INailProcessor, string, string)[] nailDesignAndVariationNames, string nailShapeName) {
			this.Avatar = avatar;
			this.NailPrefab = nailPrefab;
			this.NailDesignAndVariationNames = nailDesignAndVariationNames;
			this.NailShapeName = nailShapeName;
		}


		public void Process() {

			if (this.Backup) {
				this.CreateBackup();
			}
			
			INailProcessor.ClearCreatedMaterialCash();
			
			Undo.IncrementCurrentGroup();
			// ネイルプレハブのインスタンス化
			{
				// ネイルプレハブの置き換え処理
				Regex nailPrefabNamePattern = new(@"(?<prefix>\[.+\])(?<prefabName>.+)");
				Match match = nailPrefabNamePattern.Match(this.NailPrefab.name);
				if (match.Success) {
					string prefabName = match.Groups["prefabName"].Value;
					string prefabPath = AssetDatabase.GetAssetPath(this.NailPrefab);
					string prefabDirPath = Path.GetDirectoryName(prefabPath) ?? "";
					GameObject current = this.NailPrefab;
					using DBNailShape dbNailShape = new();
					foreach (NailShape nailShape in dbNailShape.collection) {
						string newPrefabPath = $"{prefabDirPath}/[{nailShape.ShapeName}]{prefabName}.prefab";
						if (File.Exists(newPrefabPath)) {
							GameObject newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newPrefabPath);
							if (newPrefab != null) {
								current = newPrefab;
							}
						}
						if (nailShape.ShapeName == this.NailShapeName) break;
					}

					this.NailPrefab = current;
				}
			}
			GameObject nailPrefabObject = Object.Instantiate(this.NailPrefab, this.Avatar.transform);
			nailPrefabObject.name = $"[An-Labo NailTool]{this.AvatarName}";
			Undo.RegisterCreatedObjectUndo(nailPrefabObject, "Nail Setup");

			string prefix = this.getPrefabPrefix();
			
			foreach (Transform child in nailPrefabObject.transform) {
				child.name = child.name.Replace($"{prefix}", "");
			}

			// 装着対象ボーンの取得
			Dictionary<string, Transform?> targetBoneDictionary = GetTargetBoneDictionary(this.Avatar);
			
			// プレハブ内のネイルオブジェクトを取得
			Transform?[] handsNailObjects = GetHandsNailObjectList(nailPrefabObject);
			Transform?[] leftFootNailObjects = GetLeftFootNailObjectList(nailPrefabObject);
			Transform?[] rightFootNailObjects = GetRightFootNailObjectList(nailPrefabObject);
			
			
			if (this.RemoveCurrentNail) {
				RemoveNail(this.Avatar, targetBoneDictionary);
			}
			
			// メッシュの適用
			if (this.OverrideMesh is { Length: > 0 }) {
				try {
					NailSetupUtil.ReplaceHandsNailMesh(handsNailObjects, this.OverrideMesh);
				} catch (Exception) {
					Undo.RevertAllInCurrentGroup();
					throw;
				}
			}
			
			// 足のメッシュの適用
			try {
				NailSetupUtil.ReplaceFootNailMesh(leftFootNailObjects, rightFootNailObjects, this.NailShapeName);
			} catch (Exception) {
				Undo.RevertAllInCurrentGroup();
				throw;
			}

			// マテリアルの適用
			try {
				NailSetupUtil.ReplaceNailMaterial(handsNailObjects, leftFootNailObjects, rightFootNailObjects, this.NailDesignAndVariationNames, this.NailShapeName, this.GenerateMaterial, false);
			} catch (Exception) {
				Undo.RevertAllInCurrentGroup();
				throw;
			}

			try {
				NailSetupUtil.AttachAdditionalObjects(handsNailObjects, this.NailDesignAndVariationNames, this.NailShapeName, false);
			} catch (Exception) {
				Undo.RevertAllInCurrentGroup();
				throw;
			}


			if (this.ForModularAvatar) {
				// 装着処理(ModularAvatar)
#if MD_NAIL_FOR_MA
				// 手の装着処理
				int index = -1;
				foreach (Transform? nailObject in handsNailObjects) {
					index++;
					if (nailObject == null) continue;
					ModularAvatarBoneProxy boneProxy = nailObject.gameObject.AddComponent<ModularAvatarBoneProxy>();
					boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
					boneProxy.boneReference = MDNailToolDefines.HANDS_HUMAN_BODY_BONE_LIST[index];
				}

				if (this.UseFootNail) {
					// 左足の装着処理
					
					// 左足のボーンの子に
					{
						string boneName = MDNailToolDefines.TARGET_BONE_NAME_LIST[(int)MDNailToolDefines.TargetFingerAndToe.LeftToes];
						Transform? target = targetBoneDictionary[boneName];
						if (target == null) {
							Debug.LogError($"Not found target bone : {boneName}");
						}

						foreach (Transform? nailObject in leftFootNailObjects) {
							if (nailObject == null) continue;
							ModularAvatarBoneProxy boneProxy = nailObject.gameObject.AddComponent<ModularAvatarBoneProxy>();
							boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
							boneProxy.target = target;
						}
					}


					// 右足のボーンの子に
					{
						string boneName = MDNailToolDefines.TARGET_BONE_NAME_LIST[(int)MDNailToolDefines.TargetFingerAndToe.RightToes];
						Transform? target = targetBoneDictionary[boneName];
						if (target == null) {
							Debug.LogError($"Not found target bone : {boneName}");
						}

						foreach (Transform? nailObject in rightFootNailObjects) {
							if (nailObject == null) continue;
							ModularAvatarBoneProxy boneProxy = nailObject.gameObject.AddComponent<ModularAvatarBoneProxy>();
							boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
							boneProxy.target = target;
						}
					}
					/* BoneProxyが Toeがマッピングされてないアバターに対してToeを配置してくれないため(Footのフォールバックも無い)一旦保留
					foreach (Transform? nailObject in leftFootNailObjects) {
						if (nailObject == null) continue;
						ModularAvatarBoneProxy boneProxy = nailObject.gameObject.AddComponent<ModularAvatarBoneProxy>();
						boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
						boneProxy.boneReference = MDNailToolDefines.LEFT_TOE_HUMAN_BODY_BONE;
					}

					// 右足の装着処理
					foreach (Transform? nailObject in rightFootNailObjects) {
						if (nailObject == null) continue;
						ModularAvatarBoneProxy boneProxy = nailObject.gameObject.AddComponent<ModularAvatarBoneProxy>();
						boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
						boneProxy.boneReference = MDNailToolDefines.RIGHT_TOE_HUMAN_BODY_BONE;
					}
					*/
				} else {
					// 足ネイルを装着しない場合削除
					foreach (Transform? nailObject in leftFootNailObjects) {
						if (nailObject == null) continue;
						Object.DestroyImmediate(nailObject.gameObject);
					}
					foreach (Transform? nailObject in rightFootNailObjects) {
						if (nailObject == null) continue;
						Object.DestroyImmediate(nailObject.gameObject);
					}
				}

				// 削除処理のためのマーカーコンポーネント追加
				nailPrefabObject.AddComponent<MDNailObjectMarker>();
#else
				// Modular Avatar が導入されていない場合の処理は存在しない
				// プロセスをロールバックして例外を出す
				Undo.RevertAllInCurrentGroup();
				throw new InvalidOperationException("The setup for ModularAvatar cannot be executed in environments where ModularAvatar is not installed.");
#endif
			} else {
				// 装着処理(直接)
				// 手の指のボーンの子に
				int index = -1;
				foreach (string boneName in MDNailToolDefines.TARGET_HANDS_BONE_NAME_LIST) {
					index++;

					Transform? target = targetBoneDictionary[boneName];
					if (target == null) {
						Debug.LogError($"Not found target bone : {boneName}");
						continue;
					}

					Transform? nailObject = handsNailObjects[index];
					if (nailObject == null) continue;
					nailObject.SetParent(target);
				}

				if (this.UseFootNail) {

					// 左足のボーンの子に
					{
						string boneName = MDNailToolDefines.TARGET_BONE_NAME_LIST[(int)MDNailToolDefines.TargetFingerAndToe.LeftToes];
						Transform? target = targetBoneDictionary[boneName];
						if (target == null) {
							Debug.LogError($"Not found target bone : {boneName}");
						}

						foreach (Transform? nailObject in leftFootNailObjects) {
							if (nailObject == null) continue;
							nailObject.SetParent(target);
						}
					}


					// 右足のボーンの子に
					{
						string boneName = MDNailToolDefines.TARGET_BONE_NAME_LIST[(int)MDNailToolDefines.TargetFingerAndToe.RightToes];
						Transform? target = targetBoneDictionary[boneName];
						if (target == null) {
							Debug.LogError($"Not found target bone : {boneName}");
						}

						foreach (Transform? nailObject in rightFootNailObjects) {
							if (nailObject == null) continue;
							nailObject.SetParent(target);
						}
					}
				} else {
					// 足ネイルを装着しない場合削除
					foreach (Transform? nailObject in leftFootNailObjects) {
						if (nailObject == null) continue;
						Object.DestroyImmediate(nailObject.gameObject);
					}
					foreach (Transform? nailObject in rightFootNailObjects) {
						if (nailObject == null) continue;
						Object.DestroyImmediate(nailObject.gameObject);
					}
				}

				// プレハブの元オブジェクトの削除
				Object.DestroyImmediate(nailPrefabObject);
			}
		}

		private void CreateBackup() {
			if (!Directory.Exists(MDNailToolDefines.BACKUP_PATH)) {
				Directory.CreateDirectory(MDNailToolDefines.BACKUP_PATH);
				AssetDatabase.Refresh();
			}

			GameObject clonedObject = Object.Instantiate(this.Avatar.gameObject);
			string prefabName = $"bk_{this.Avatar.gameObject.name}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.prefab";
			PrefabUtility.SaveAsPrefabAsset(clonedObject, MDNailToolDefines.BACKUP_PATH + prefabName);
			AssetDatabase.Refresh();
			Object.DestroyImmediate(clonedObject);
		}
		


		private string getPrefabPrefix() {
			Regex regex = new(@"(?<prefix>\[.+\]).+");
			Match match = regex.Match(this.NailPrefab.name);
			if (match.Success) return match.Groups["prefix"].Value;

			Debug.LogError("Failed to obtain nail prefix.", this.NailPrefab);
			return "";
		}

		public static void RemoveNail(VRCAvatarDescriptor avatar, Dictionary<string, Transform?>? targetBoneDictionary = null) {
			targetBoneDictionary ??= GetTargetBoneDictionary(avatar);
			
			// 既存のネイル削除処理
			// ModularAvatar用の削除処理
			foreach (MDNailObjectMarker mdNailObjectMarker in avatar.GetComponentsInChildren<MDNailObjectMarker>().ToArray()) {
				Undo.DestroyObjectImmediate(mdNailObjectMarker.gameObject);
			}

			// 直接装着された物の削除処理
			// 手のネイル削除処理
			int index = -1;
			foreach (string boneName in MDNailToolDefines.TARGET_HANDS_BONE_NAME_LIST) {
				index++;
				string objectName = MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST[index];
				Transform? targetBone = targetBoneDictionary[boneName];
				if (targetBone == null) continue;
				Transform?[] nailObjects = targetBone.transform
					.FindRecursiveWithRegex($@"(\[.+\])?{Regex.Escape(objectName)}")
					.ToArray();
				foreach (Transform? nailObject in nailObjects) {
					if (nailObject == null) continue;
					Undo.DestroyObjectImmediate(nailObject.gameObject);
				}
			}

			// 左足のネイル削除処理
			{
				Transform? targetBone = targetBoneDictionary[MDNailToolDefines.LEFT_TOES];
				if (targetBone != null) {
					foreach (string objectName in MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST) {
						Transform?[] nailObjects = targetBone.transform
							.FindRecursiveWithRegex(Regex.Escape(objectName))
							.ToArray();
						foreach (Transform? nailObject in nailObjects) {
							if (nailObject == null) continue;
							Undo.DestroyObjectImmediate(nailObject.gameObject);
						}
					}
				}
			}

			// 右足のネイル削除処理
			{
				Transform? targetBone = targetBoneDictionary[MDNailToolDefines.RIGHT_TOES];
				if (targetBone == null) return;
				foreach (string objectName in MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST) {
					Transform?[] nailObjects = targetBone.transform
						.FindRecursiveWithRegex(Regex.Escape(objectName))
						.ToArray();
					foreach (Transform? nailObject in nailObjects) {
						if (nailObject == null) continue;
						Undo.DestroyObjectImmediate(nailObject.gameObject);
					}
				}
			}
		}

		private static Dictionary<string, Transform?> GetTargetBoneDictionary(VRCAvatarDescriptor avatar) {
			Animator? avatarAnimator = avatar.GetComponent<Animator>();
			Avatar? animatorAvatar = avatarAnimator.avatar;
			HumanDescription humanDescription = animatorAvatar.humanDescription;
			HumanBone[] humanBones = humanDescription.human;
			Dictionary<string, string> boneNameDictionary = humanBones.ToDictionary(humanBone => humanBone.humanName, humanBone => humanBone.boneName);
			return MDNailToolDefines.TARGET_BONE_NAME_LIST
				.Select(name => {
					if (name is not (MDNailToolDefines.LEFT_TOES or MDNailToolDefines.RIGHT_TOES)) {
						// 通常はつま先同様、ボーンが未マップを想定するべきだが、指が未マップのアバターは普通存在しないため、エラーを出させるために処理を分ける。
						return (name, avatar.transform.FindRecursive(boneNameDictionary[name]));
					}
					
					// つま先がアバターにマッピングされていないアバターがあった。
					// そもそもつま先がないアバターがありそうなため、つま先がない場合足を取得する
					string footBoneName = name switch {
						MDNailToolDefines.LEFT_TOES => MDNailToolDefines.LEFT_FOOT,
						MDNailToolDefines.RIGHT_TOES => MDNailToolDefines.RIGHT_FOOT,
						_ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
					};
					string? targetBoneName = boneNameDictionary.GetValueOrDefault(name);
					targetBoneName ??= boneNameDictionary.GetValueOrDefault(footBoneName, "");
					return (name, avatar.transform.FindRecursive(targetBoneName));

				})
				.ToDictionary(tuple => tuple.name, tuple => tuple.Item2);
		}

		private static Transform?[] GetHandsNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST
				.Select(name => nailPrefabObject.transform.Find(name))
				.ToArray();
		}

		private static Transform?[] GetLeftFootNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
				.Select(name => nailPrefabObject.transform.Find(name))
				.ToArray();
		}

		private static Transform?[] GetRightFootNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST
				.Select(name => nailPrefabObject.transform.Find(name))
				.ToArray();
		}
	}
}
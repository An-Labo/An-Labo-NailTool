using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Runtime;
using world.anlabo.mdnailtool.Runtime.Extensions;
using UEAvatar = UnityEngine.Avatar;

#if MD_NAIL_FOR_MA
using nadena.dev.modular_avatar.core;
#endif

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public partial class NailSetupProcessor {
		public static void RemoveNail(VRCAvatarDescriptor avatar, IReadOnlyDictionary<string, string>? boneMappingOverride) {
			Dictionary<string, Transform?> targetBoneDictionary = GetTargetBoneDictionary(avatar, boneMappingOverride);
			RemoveNail(avatar, targetBoneDictionary);
		}

		private static void RemoveNail(VRCAvatarDescriptor avatar, Dictionary<string, Transform?> targetBoneDictionary) {
			foreach (MDNailObjectMarker mdNailObjectMarker in avatar.GetComponentsInChildren<MDNailObjectMarker>().ToArray()) {
				Undo.DestroyObjectImmediate(mdNailObjectMarker.gameObject);
			}

			int index = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
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

			index = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
			int objectIndex = -1;
			foreach (string boneName in MDNailToolDefines.LEFT_FOOT_FINGER_BONE_NAME_LIST) {
				index++;
				objectIndex++;
				if (objectIndex >= 5) continue;
				string objectName = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST[objectIndex];
				Transform? targetBone = targetBoneDictionary[boneName];
				if (targetBone == null) continue;
				Transform?[] nailObjects = targetBone.transform
					.FindRecursiveWithRegex(Regex.Escape(objectName))
					.ToArray();
				foreach (Transform? nailObject in nailObjects) {
					if (nailObject == null) continue;
					Undo.DestroyObjectImmediate(nailObject.gameObject);
				}
			}

			index = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
			objectIndex = -1;
			foreach (string boneName in MDNailToolDefines.RIGHT_FOOT_FINGER_BONE_NAME_LIST) {
				index++;
				objectIndex++;
				if (objectIndex >= 5) continue;
				string objectName = MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST[objectIndex];
				Transform? targetBone = targetBoneDictionary[boneName];
				if (targetBone == null) continue;
				Transform?[] nailObjects = targetBone.transform
					.FindRecursiveWithRegex(Regex.Escape(objectName))
					.ToArray();
				foreach (Transform? nailObject in nailObjects) {
					if (nailObject == null) continue;
					Undo.DestroyObjectImmediate(nailObject.gameObject);
				}
			}
			// [An-Labo] 親が空になったら削除 (NailTool 管理 GO のみ対象, 同名の他ツール GO 誤爆防止).
			Transform? anLaboParent = avatar.transform.Find("[An-Labo]");
			if (anLaboParent != null
				&& anLaboParent.childCount == 0
#if MD_NAIL_FOR_MA
				&& anLaboParent.GetComponent<ModularAvatarMenuInstaller>() != null
#endif
				) {
				Undo.DestroyObjectImmediate(anLaboParent.gameObject);
			}
		}

		internal static Dictionary<string, Transform?> GetTargetBoneDictionary(VRCAvatarDescriptor avatar, IReadOnlyDictionary<string, string>? boneMappingOverride) {
			Animator? avatarAnimator = avatar.GetComponent<Animator>();
			UEAvatar? animatorAvatar = avatarAnimator != null ? avatarAnimator.avatar : null;
			bool isHumanoid = animatorAvatar != null && animatorAvatar.isHuman;

			Dictionary<string, string> boneNameDictionary;
			if (isHumanoid) {
				HumanBone[] humanBones = animatorAvatar!.humanDescription.human;
				boneNameDictionary = humanBones.ToDictionary(humanBone => humanBone.humanName, humanBone => humanBone.boneName);
			} else {
				boneNameDictionary = new Dictionary<string, string>();
			}

			// 素体 Armature root を 3 段階フォールバックで特定する (衣装/髪型 Armature に侵入させないための物理境界).
			Transform searchRoot = ResolveSearchRoot(avatar, avatarAnimator, isHumanoid);

			// Humanoid アバターは Animator.GetBoneTransform を最優先で使い、boneName 衝突を完全に回避する.
			return MDNailToolDefines.TARGET_BONE_NAME_LIST
				.Select(name => {
					int handIndex = ((IList<string>)MDNailToolDefines.TARGET_HANDS_BONE_NAME_LIST).IndexOf(name);
					if (handIndex >= 0) {
						if (boneMappingOverride != null && boneMappingOverride.TryGetValue(name, out string handFingerBonePath)) {
							Transform? targetBone = avatar.transform.Find(handFingerBonePath);
							if (targetBone != null) {
								return (name, targetBone);
							}

							ToolConsole.Warn("NailSetup", $"Not found bone : {handFingerBonePath}");
						}

						if (isHumanoid) {
							Transform? mapped = avatarAnimator!.GetBoneTransform(MDNailToolDefines.HANDS_HUMAN_BODY_BONE_LIST[handIndex]);
							if (mapped != null) {
								return (name, (Transform?)mapped);
							}
						}

						string handTargetName = boneNameDictionary.GetValueOrDefault(name, name);
						return (name, searchRoot.FindRecursive(handTargetName));
					}


					if (boneMappingOverride != null && boneMappingOverride.TryGetValue(name, out string footFingerBonePath)) {
						Transform? targetBone = avatar.transform.Find(footFingerBonePath);
						if (targetBone != null) {
							return (name, targetBone);
						}

						ToolConsole.Warn("NailSetup", $"Not found bone : {footFingerBonePath}");
					}

					string toeBoneName = MDNailToolDefines.LEFT_FOOT_FINGER_BONE_NAME_LIST.Contains(name) ? MDNailToolDefines.LEFT_TOES : MDNailToolDefines.RIGHT_TOES;

					if (isHumanoid) {
						HumanBodyBones toeBodyBone = toeBoneName == MDNailToolDefines.LEFT_TOES ? MDNailToolDefines.LEFT_TOE_HUMAN_BODY_BONE : MDNailToolDefines.RIGHT_TOE_HUMAN_BODY_BONE;
						Transform? toe = avatarAnimator!.GetBoneTransform(toeBodyBone);
						if (toe != null) {
							return (name, (Transform?)toe);
						}

						HumanBodyBones footBodyBone = toeBoneName == MDNailToolDefines.LEFT_TOES ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
						Transform? foot = avatarAnimator.GetBoneTransform(footBodyBone);
						if (foot != null) {
							return (name, (Transform?)foot);
						}
					}

					string footBoneName = toeBoneName switch {
						MDNailToolDefines.LEFT_TOES => MDNailToolDefines.LEFT_FOOT,
						MDNailToolDefines.RIGHT_TOES => MDNailToolDefines.RIGHT_FOOT,
						_ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
					};

					string? targetBoneName = boneNameDictionary.GetValueOrDefault(toeBoneName);
					targetBoneName ??= boneNameDictionary.GetValueOrDefault(footBoneName, "");
					return (name, searchRoot.FindRecursive(targetBoneName));
				})
				.ToDictionary(tuple => tuple.name, tuple => tuple.Item2);
		}

		// 素体 Armature の root Transform を特定する. Humanoid なら Hips、未設定なら Eye Look Bone、最後に体メッシュ bones[] の先祖を辿って avatar 直下まで上がる.
		private static Transform ResolveSearchRoot(VRCAvatarDescriptor avatar, Animator? animator, bool isHumanoid) {
			Transform? candidate = null;

			if (isHumanoid && animator != null) {
				Transform? hips = animator.GetBoneTransform(HumanBodyBones.Hips);
				if (hips != null) {
					candidate = AscendToAvatarChild(hips, avatar.transform);
				}
			}

			if (candidate == null && avatar.enableEyeLook) {
				Transform? leftEye = avatar.customEyeLookSettings.leftEye;
				Transform? rightEye = avatar.customEyeLookSettings.rightEye;
				Transform? eye = leftEye != null ? leftEye : rightEye;
				if (eye != null) {
					candidate = AscendToAvatarChild(eye, avatar.transform);
				}
			}

			if (candidate == null) {
				SkinnedMeshRenderer? bodySmr = FindBodyMesh(avatar);
				if (bodySmr != null && bodySmr.bones.Length > 0) {
					Transform? anyBone = bodySmr.bones.FirstOrDefault(b => b != null);
					if (anyBone != null) {
						candidate = AscendToAvatarChild(anyBone, avatar.transform);
					}
				}
			}

			// 全フォールバック失敗時は avatar.transform に落とす (旧実装と同等). 衣装同名ボーン衝突リスクが残るが、それ以上の手がかりが無い以上 boneMappingOverride 手動指定に委ねる.
			return candidate != null ? candidate : avatar.transform;
		}

		// `bone` から `avatarRoot` の直接の子に到達するまで親方向へ辿る. 親辿りの途中で root に到達した場合は bone 自身を返す.
		private static Transform AscendToAvatarChild(Transform bone, Transform avatarRoot) {
			Transform current = bone;
			while (current.parent != null && current.parent != avatarRoot) {
				current = current.parent;
			}
			return current.parent == avatarRoot ? current : bone;
		}

		// 素体メッシュ候補の SkinnedMeshRenderer を返す. Viseme 以外で BlendShape を最多に持つ SMR を選ぶ (NailSetupProcessor.BlendShape.cs のフォールバックと同じ判定).
		private static SkinnedMeshRenderer? FindBodyMesh(VRCAvatarDescriptor avatar) {
			SkinnedMeshRenderer? visemeSmr = avatar.VisemeSkinnedMesh;
			return avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
				.Where(smr => smr != visemeSmr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
				.OrderByDescending(smr => smr.sharedMesh!.blendShapeCount)
				.FirstOrDefault();
		}
	}
}

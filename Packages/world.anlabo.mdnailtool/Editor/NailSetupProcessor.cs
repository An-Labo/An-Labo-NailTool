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
using UEAvatar = UnityEngine.Avatar;

#if MD_NAIL_FOR_MA
using nadena.dev.modular_avatar.core;
#endif

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public class NailSetupProcessor {
		private VRCAvatarDescriptor Avatar { get; }
		private AvatarVariation AvatarVariationData { get; }
		private GameObject NailPrefab { get; set; }
		private (INailProcessor, string, string)[] NailDesignAndVariationNames { get; }
		private string NailShapeName { get; }
		public Mesh?[]? OverrideMesh { get; set; }
		public Material? OverrideMaterial { get; set; }
		public string AvatarName { get; set; } = null!;
		public bool UseFootNail { get; set; }
		public bool RemoveCurrentNail { get; set; }
		public bool GenerateMaterial { get; set; }
		public bool Backup { get; set; }
		public bool ForModularAvatar { get; set; }
	public bool GenerateExpressionMenu { get; set; }
	public bool SplitHandFoot { get; set; }
	public bool MergeAnLabo { get; set; }

		public NailSetupProcessor(VRCAvatarDescriptor avatar, AvatarVariation avatarVariationData, GameObject nailPrefab, (INailProcessor, string, string)[] nailDesignAndVariationNames,
			string nailShapeName) {
			this.Avatar = avatar;
			this.AvatarVariationData = avatarVariationData;
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
			{
				string designName = this.NailDesignAndVariationNames.Length > 0
					? this.NailDesignAndVariationNames[0].Item1.DesignName
					: "Unknown";
				string colorName = this.NailDesignAndVariationNames.Length > 0
					? this.NailDesignAndVariationNames[0].Item3
					: "";
				string nailLabel = string.IsNullOrEmpty(colorName) ? designName : $"{designName}_{colorName}";
				nailPrefabObject.name = $"[An-Labo]{nailLabel}";
			}
			Undo.RegisterCreatedObjectUndo(nailPrefabObject, "Nail Setup");

			string prefix = this.getPrefabPrefix();

			foreach (Transform child in nailPrefabObject.transform) {
				child.name = child.name.Replace($"{prefix}", "");
			}

			// 装着対象ボーンの取得
			Dictionary<string, Transform?> targetBoneDictionary = GetTargetBoneDictionary(this.Avatar, this.AvatarVariationData.BoneMappingOverride);

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
				NailSetupUtil.ReplaceNailMaterial(handsNailObjects, leftFootNailObjects, rightFootNailObjects, this.NailDesignAndVariationNames, this.NailShapeName, this.GenerateMaterial, false, this.OverrideMaterial);
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
#if MD_NAIL_FOR_MA
				string variationName = this.AvatarVariationData.VariationName;
				string handWrapperName = $"HandNail_{variationName}";
				string footWrapperName = $"FootNail_{variationName}";

				// ---- HandNailラッパー作成 ----
				GameObject handWrapper = new GameObject(handWrapperName);
				handWrapper.transform.SetParent(nailPrefabObject.transform, false);
				foreach (Transform? nailObject in handsNailObjects)
				{
					if (nailObject == null) continue;
					nailObject.SetParent(handWrapper.transform, false);
				}

				// ---- FootNailラッパー作成 ----
				GameObject? footWrapper = null;
				if (this.UseFootNail)
				{
					footWrapper = new GameObject(footWrapperName);
					footWrapper.transform.SetParent(nailPrefabObject.transform, false);
					foreach (Transform? nailObject in leftFootNailObjects)
					{
						if (nailObject == null) continue;
						nailObject.SetParent(footWrapper.transform, false);
					}
					foreach (Transform? nailObject in rightFootNailObjects)
					{
						if (nailObject == null) continue;
						nailObject.SetParent(footWrapper.transform, false);
					}
				}
				else
				{
					foreach (Transform? nailObject in leftFootNailObjects)
						if (nailObject != null) Object.DestroyImmediate(nailObject.gameObject);
					foreach (Transform? nailObject in rightFootNailObjects)
						if (nailObject != null) Object.DestroyImmediate(nailObject.gameObject);
				}

				// ---- BoneProxy設定（各ネイルオブジェクトに）----
				int index = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
				foreach (Transform? nailObject in handsNailObjects)
				{
					index++;
					if (nailObject == null) continue;
					ModularAvatarBoneProxy boneProxy = nailObject.gameObject.AddComponent<ModularAvatarBoneProxy>();
					boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
					boneProxy.target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
				}

				if (this.UseFootNail)
				{
					index = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
					foreach (Transform? nailObject in leftFootNailObjects)
					{
						index++;
						if (nailObject == null) continue;
						ModularAvatarBoneProxy boneProxy = nailObject.gameObject.AddComponent<ModularAvatarBoneProxy>();
						boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
						boneProxy.target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
					}
					index = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
					foreach (Transform? nailObject in rightFootNailObjects)
					{
						index++;
						if (nailObject == null) continue;
						ModularAvatarBoneProxy boneProxy = nailObject.gameObject.AddComponent<ModularAvatarBoneProxy>();
						boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
						boneProxy.target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
					}
				}

				// ---- re-wear / [An-Labo]まとめ処理 ----
				if (this.MergeAnLabo)
				{
					Transform? anLaboParent = this.Avatar.transform.Find("[An-Labo]");
					if (anLaboParent == null)
					{
						GameObject anLaboObj = new GameObject("[An-Labo]");
						anLaboObj.transform.SetParent(this.Avatar.transform, false);
						Undo.RegisterCreatedObjectUndo(anLaboObj, "Nail Setup");
						anLaboParent = anLaboObj.transform;
					}
					Transform? existingNailRoot = anLaboParent.Find(nailPrefabObject.name);
					if (existingNailRoot != null && existingNailRoot.GetComponent<MDNailObjectMarker>() != null)
					{
						handWrapper.transform.SetParent(existingNailRoot, false);
						if (footWrapper != null) footWrapper.transform.SetParent(existingNailRoot, false);
						Object.DestroyImmediate(nailPrefabObject);
						nailPrefabObject = existingNailRoot.gameObject;
					}
					else
					{
						nailPrefabObject.transform.SetParent(anLaboParent, false);
					}
				}
				else
				{
					Transform? existingRoot = this.Avatar.transform.Find(nailPrefabObject.name);
					if (existingRoot != null && existingRoot.GetComponent<MDNailObjectMarker>() != null)
					{
						handWrapper.transform.SetParent(existingRoot, false);
						if (footWrapper != null) footWrapper.transform.SetParent(existingRoot, false);
						Object.DestroyImmediate(nailPrefabObject);
						nailPrefabObject = existingRoot.gameObject;
					}
				}

				// ---- マーカーコンポーネント追加（重複付与防止）----
				if (nailPrefabObject.GetComponent<MDNailObjectMarker>() == null)
					nailPrefabObject.AddComponent<MDNailObjectMarker>();

				if (this.GenerateExpressionMenu)
					this.SetupExpressionMenu(nailPrefabObject);
#else
				Undo.RevertAllInCurrentGroup();
				throw new InvalidOperationException("The setup for ModularAvatar cannot be executed in environments where ModularAvatar is not installed.");
#endif
			} else {
				int index = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
				foreach (Transform? nailObject in handsNailObjects) {
					index++;
					if (nailObject == null) continue;
					Transform? target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
					if (target == null) {
						Debug.LogError($"Not found target bone : {MDNailToolDefines.TARGET_BONE_NAME_LIST[index]}");
						continue;
					}

					nailObject.SetParent(target);
				}

				if (this.UseFootNail) {
					index = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
					foreach (Transform? nailObject in leftFootNailObjects) {
						index++;
						if (nailObject == null) continue;
						Transform? target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
						if (target == null) {
							Debug.LogError($"Not found target bone : {MDNailToolDefines.TARGET_BONE_NAME_LIST[index]}");
							continue;
						}

						nailObject.SetParent(target);
					}

					foreach (Transform? nailObject in rightFootNailObjects) {
						index++;
						if (nailObject == null) continue;
						Transform? target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
						if (target == null) {
							Debug.LogError($"Not found target bone : {MDNailToolDefines.TARGET_BONE_NAME_LIST[index]}");
							continue;
						}

						nailObject.SetParent(target);
					}
				} else {
					foreach (Transform? nailObject in leftFootNailObjects) {
						if (nailObject == null) continue;
						Object.DestroyImmediate(nailObject.gameObject);
					}

					foreach (Transform? nailObject in rightFootNailObjects) {
						if (nailObject == null) continue;
						Object.DestroyImmediate(nailObject.gameObject);
					}
				}

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
		}

		private static Dictionary<string, Transform?> GetTargetBoneDictionary(VRCAvatarDescriptor avatar, IReadOnlyDictionary<string, string>? boneMappingOverride) {
			Animator? avatarAnimator = avatar.GetComponent<Animator>();
			UEAvatar? animatorAvatar = avatarAnimator.avatar;
			HumanDescription humanDescription = animatorAvatar.humanDescription;
			HumanBone[] humanBones = humanDescription.human;
			Dictionary<string, string> boneNameDictionary = humanBones.ToDictionary(humanBone => humanBone.humanName, humanBone => humanBone.boneName);
			return MDNailToolDefines.TARGET_BONE_NAME_LIST
				.Select(name => {
					if (MDNailToolDefines.TARGET_HANDS_BONE_NAME_LIST.Contains(name)) {
						// 通常はつま先同様、ボーンが未マップを想定するべきだが、指が未マップのアバターは普通存在しないため、エラーを出させるために処理を分ける。
						// ReSharper disable once InvertIf
						if (boneMappingOverride != null && boneMappingOverride.TryGetValue(name, out string handFingerBonePath)) {
							Transform? targetBone = avatar.transform.Find(handFingerBonePath);
							if (targetBone != null) {
								return (name, targetBone);
							}

							Debug.LogWarning($"Not found bone : {handFingerBonePath}");
						}

						return (name, avatar.transform.FindRecursive(boneNameDictionary[name]));
					}


					if (boneMappingOverride != null && boneMappingOverride.TryGetValue(name, out string footFingerBonePath)) {
						// ボーンが上書きされていればそれを反す
						Transform? targetBone = avatar.transform.Find(footFingerBonePath);
						if (targetBone != null) {
							return (name, targetBone);
						}

						Debug.LogWarning($"Not found bone : {footFingerBonePath}");
					}

					// 足の指のボーン名から、どちらのつま先かを求める
					string toeBoneName = MDNailToolDefines.LEFT_FOOT_FINGER_BONE_NAME_LIST.Contains(name) ? MDNailToolDefines.LEFT_TOES : MDNailToolDefines.RIGHT_TOES;

					// つま先がアバターにマッピングされていないアバターがあった。
					// そもそもつま先がないアバターがありそうなため、つま先がない場合足を取得する
					string footBoneName = toeBoneName switch {
						MDNailToolDefines.LEFT_TOES => MDNailToolDefines.LEFT_FOOT,
						MDNailToolDefines.RIGHT_TOES => MDNailToolDefines.RIGHT_FOOT,
						_ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
					};

					string? targetBoneName = boneNameDictionary.GetValueOrDefault(toeBoneName);
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

#if MD_NAIL_FOR_MA
	private void SetupExpressionMenu(GameObject nailRoot) {
		string designName = this.NailDesignAndVariationNames.Length > 0
			? this.NailDesignAndVariationNames[0].Item1.DesignName : "Nail";
		string colorName = this.NailDesignAndVariationNames.Length > 0
			? this.NailDesignAndVariationNames[0].Item3 : "";
		string menuLabel = string.IsNullOrEmpty(colorName) ? designName : $"{designName}_{colorName}";
		string variationName = this.AvatarVariationData.VariationName;
		string handWrapperName = $"HandNail_{variationName}";
		string footWrapperName = $"FootNail_{variationName}";

		// サムネイル取得
		Texture2D? thumbnail = null;
		{
			using DBNailDesign db = new();
			NailDesign? design = db.FindNailDesignByDesignName(designName);
			if (design != null && !string.IsNullOrEmpty(design.ThumbnailGUID)) {
				string path = AssetDatabase.GUIDToAssetPath(design.ThumbnailGUID);
				if (!string.IsNullOrEmpty(path))
					thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
			}
		}

		// ---- MergeAnLabo: [An-Labo]親にMenuInstaller+SubMenuItem（まだ無ければ）----
		if (this.MergeAnLabo) {
			GameObject anLaboObj = nailRoot.transform.parent?.gameObject ?? this.Avatar.gameObject;
			if (anLaboObj.GetComponent<ModularAvatarMenuInstaller>() == null) {
				// [An-Labo]新規作成時: 最初のネイルのサムネイルを設定
				anLaboObj.AddComponent<ModularAvatarMenuInstaller>();
				var anLaboMenuItem = anLaboObj.AddComponent<ModularAvatarMenuItem>();
				anLaboMenuItem.PortableControl.Type = PortableControlType.SubMenu;
				anLaboMenuItem.PortableControl.Icon = thumbnail;
				anLaboMenuItem.label = "An-Labo";
				anLaboMenuItem.MenuSource = SubmenuSource.Children;
			} else {
				// [An-Labo]が既存（2本目以降のネイル追加時）: アイコンをnullに更新
				var existingMenuItem = anLaboObj.GetComponent<ModularAvatarMenuItem>();
				if (existingMenuItem != null)
					existingMenuItem.PortableControl.Icon = null;
			}
		} else {
			// MergeAnLabo=false: nailRoot自身にMenuInstaller
			if (nailRoot.GetComponent<ModularAvatarMenuInstaller>() == null)
				nailRoot.AddComponent<ModularAvatarMenuInstaller>();
		}

		// ---- nailRoot に MenuItem ----
		ModularAvatarMenuItem rootMenuItem = nailRoot.AddComponent<ModularAvatarMenuItem>();
		rootMenuItem.PortableControl.Icon = thumbnail;
		rootMenuItem.label = menuLabel;
		if (this.SplitHandFoot) {
			rootMenuItem.PortableControl.Type = PortableControlType.SubMenu;
			rootMenuItem.MenuSource = SubmenuSource.Children;
		} else {
			// SplitHandFoot=OFF: nailRoot自体にObjectToggle（HandNail/FootNailラッパーをまとめてON/OFF）
			rootMenuItem.PortableControl.Type = PortableControlType.Toggle;
			rootMenuItem.PortableControl.Value = 1;
			rootMenuItem.isSaved = true;
			rootMenuItem.isSynced = true;
			rootMenuItem.automaticValue = true;

			ModularAvatarObjectToggle rootToggle = nailRoot.AddComponent<ModularAvatarObjectToggle>();
			var toggleTargets = new System.Collections.Generic.List<ToggledObject>();
			// HandNailラッパー内の各ネイルオブジェクトを個別に登録
			Transform? hw = nailRoot.transform.Find(handWrapperName);
			if (hw != null) {
				foreach (Transform child in hw)
					toggleTargets.Add(new ToggledObject { Object = new AvatarObjectReference(child.gameObject), Active = false });
			}
			// FootNailラッパー内の各ネイルオブジェクトを個別に登録
			if (this.UseFootNail) {
				Transform? fw = nailRoot.transform.Find(footWrapperName);
				if (fw != null) {
					foreach (Transform child in fw)
						toggleTargets.Add(new ToggledObject { Object = new AvatarObjectReference(child.gameObject), Active = false });
				}
			}
			rootToggle.Objects = toggleTargets;
		}

		// ---- SplitHandFoot=ON: HandNail/FootNailにObjectToggle+MenuItem（アイコンなし）----
		if (this.SplitHandFoot) {
			Transform? handWrapperT = nailRoot.transform.Find(handWrapperName);
			if (handWrapperT != null) {
				ModularAvatarObjectToggle handToggle = handWrapperT.gameObject.AddComponent<ModularAvatarObjectToggle>();
				handToggle.Objects = handWrapperT.Cast<Transform>()
					.Select(t => new ToggledObject {
						Object = new AvatarObjectReference(t.gameObject),
						Active = false
					})
					.ToList();

				ModularAvatarMenuItem handMenuItem = handWrapperT.gameObject.AddComponent<ModularAvatarMenuItem>();
				handMenuItem.PortableControl.Type = PortableControlType.Toggle;
				handMenuItem.PortableControl.Value = 1;
				handMenuItem.PortableControl.Icon = null;
				handMenuItem.isSaved = true;
				handMenuItem.isSynced = true;
				handMenuItem.automaticValue = true;
				handMenuItem.label = $"HandNail - {variationName}";
			}

			if (this.UseFootNail) {
				Transform? footWrapperT = nailRoot.transform.Find(footWrapperName);
				if (footWrapperT != null) {
					ModularAvatarObjectToggle footToggle = footWrapperT.gameObject.AddComponent<ModularAvatarObjectToggle>();
					footToggle.Objects = footWrapperT.Cast<Transform>()
						.Select(t => new ToggledObject {
							Object = new AvatarObjectReference(t.gameObject),
							Active = false
						})
						.ToList();

					ModularAvatarMenuItem footMenuItem = footWrapperT.gameObject.AddComponent<ModularAvatarMenuItem>();
					footMenuItem.PortableControl.Type = PortableControlType.Toggle;
					footMenuItem.PortableControl.Value = 1;
					footMenuItem.PortableControl.Icon = null;
					footMenuItem.isSaved = true;
					footMenuItem.isSynced = true;
					footMenuItem.automaticValue = true;
					footMenuItem.label = $"FootNail - {variationName}";
				}
			}
		}
	}
#endif
	}
}
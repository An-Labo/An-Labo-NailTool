using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using Object = UnityEngine.Object;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Runtime;
using world.anlabo.mdnailtool.Runtime.Extensions;
using UEAvatar = UnityEngine.Avatar;

#if MD_NAIL_FOR_MA
using nadena.dev.modular_avatar.core;
#endif

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	/// <summary>
	/// ユーザー向けエラーメッセージを持つ例外
	/// </summary>
	public class NailSetupUserException : Exception {
		public NailSetupUserException(string message) : base(message) { }
	}

	public class NailSetupProcessor {
		private VRCAvatarDescriptor Avatar { get; }
		private AvatarVariation AvatarVariationData { get; }
		private GameObject NailPrefab { get; set; }
		private (INailProcessor, string, string)[] NailDesignAndVariationNames { get; }
		private string NailShapeName { get; }
		public Mesh?[]? OverrideMesh { get; set; }
		public Material? OverrideMaterial { get; set; }
		public string? AvatarName { get; set; }
		public bool UseFootNail { get; set; }
		public bool RemoveCurrentNail { get; set; }
		public bool GenerateMaterial { get; set; }
		public bool Backup { get; set; }
		public bool ForModularAvatar { get; set; }
	public bool GenerateExpressionMenu { get; set; }
	public bool SplitHandFoot { get; set; }
	public bool MergeAnLabo { get; set; }
		public bool ArmatureScaleCompensation { get; set; }
		public bool BakeBlendShapes { get; set; }
		public bool SyncBlendShapesWithMA { get; set; }
		public string? SelectedBlendShapeVariantName { get; set; }
		public Entity.Avatar? AvatarEntity { get; set; }
		public bool EnablePenetrationCorrection { get; set; }
		public bool EnableAdditionalMaterials { get; set; } = true;
		public IEnumerable<Material>?[]? PerFingerAdditionalMaterials { get; set; }
		public IEnumerable<Transform>?[]? PerFingerAdditionalObjects { get; set; }

		/// <summary>Process中に発生した非致命的な警告メッセージ</summary>
		public List<string> Warnings { get; } = new();

		public NailSetupProcessor(VRCAvatarDescriptor avatar, AvatarVariation avatarVariationData, GameObject nailPrefab, (INailProcessor, string, string)[] nailDesignAndVariationNames,
			string nailShapeName) {
			this.Avatar = avatar;
			this.AvatarVariationData = avatarVariationData;
			this.NailPrefab = nailPrefab;
			this.NailDesignAndVariationNames = nailDesignAndVariationNames;
			this.NailShapeName = nailShapeName;
		}


		public void Process() {
			// Animatorチェック
			Animator avatarAnimator = this.Avatar.GetComponent<Animator>();
			if (avatarAnimator == null) {
				throw new NailSetupUserException(LanguageManager.S("error.execute.no_animator") ?? "error.execute.no_animator");
			}
			if (avatarAnimator.avatar == null) {
				throw new NailSetupUserException(LanguageManager.S("error.execute.no_avatar_rig") ?? "error.execute.no_avatar_rig");
			}

			INailProcessor.ClearCreatedMaterialCash();

			Undo.IncrementCurrentGroup();

			// ドロップダウンでバリアントが選択されている場合、ベースのNailPrefabを差し替える
			ToolConsole.Log($"  SelectedBlendShapeVariantName={this.SelectedBlendShapeVariantName ?? "(null)"}");
			if (!string.IsNullOrEmpty(this.SelectedBlendShapeVariantName))
			{
				AvatarBlendShapeVariant[]? activeVariants = this.AvatarVariationData.BlendShapeVariants ?? this.AvatarEntity?.BlendShapeVariants;
				ToolConsole.Log($"  activeVariants null? {activeVariants == null}, count={activeVariants?.Length ?? 0}");
				if (activeVariants != null)
				{
					ToolConsole.Log($"  activeVariants names: [{string.Join(", ", activeVariants.Select(v => v.Name))}]");
					AvatarBlendShapeVariant variant = activeVariants.FirstOrDefault(v => v.Name == this.SelectedBlendShapeVariantName);
					ToolConsole.Log($"  variant match? {variant != null}, GUID={variant?.NailPrefabGUID ?? "(null)"}");
					if (variant != null && !string.IsNullOrEmpty(variant.NailPrefabGUID))
					{
						string? variantPath = ResolveVariantPath(variant);
						ToolConsole.Log($"  variantPath={variantPath ?? "(null)"}");
						if (!string.IsNullOrEmpty(variantPath))
						{
							GameObject? variantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
							ToolConsole.Log($"  variantPrefab={variantPrefab?.name ?? "(null)"}");
							if (variantPrefab != null)
							{
								this.NailPrefab = variantPrefab;
								ToolConsole.Log($"  → NailPrefab replaced: {variantPrefab.name}");
							}
						}
					}
				}
			}
			else
			{
				ToolConsole.Log("  → no variant selected, using base prefab");
			}

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
			if (this.NailPrefab == null) {
				throw new NailSetupUserException(LanguageManager.S("error.execute.nail_prefab_load_failed") ?? "error.execute.nail_prefab_load_failed");
			}
			GameObject nailPrefabObject = Object.Instantiate(this.NailPrefab, this.Avatar.transform);
			{
				var firstEntry = this.NailDesignAndVariationNames.FirstOrDefault(t => t.Item1 != null);
				string designName = firstEntry.Item1 != null
					? firstEntry.Item1.DesignName
					: (this.OverrideMaterial?.name ?? "Unknown");
				string colorName = firstEntry.Item1 != null ? firstEntry.Item3 : "";
				string nailLabel = string.IsNullOrEmpty(colorName) ? designName : $"{designName}_{colorName}";
				nailPrefabObject.name = $"[An-Labo]{nailLabel}";
			}
			Undo.RegisterCreatedObjectUndo(nailPrefabObject, "Nail Setup");

			string prefix = this.getPrefabPrefix();

			if (!string.IsNullOrEmpty(prefix)) {
				foreach (Transform child in nailPrefabObject.transform) {
					child.name = child.name.Replace(prefix, "");
				}
			}

			// 装着対象ボーンの取得
			Dictionary<string, Transform?> targetBoneDictionary = GetTargetBoneDictionary(this.Avatar, this.AvatarVariationData.BoneMappingOverride);

			// 指ボーン存在チェック
			bool hasAnyFingerBone = MDNailToolDefines.TARGET_HANDS_BONE_NAME_LIST
				.Any(name => targetBoneDictionary.ContainsKey(name) && targetBoneDictionary[name] != null);
			if (!hasAnyFingerBone) {
				throw new NailSetupUserException(LanguageManager.S("error.execute.no_finger_bones") ?? "error.execute.no_finger_bones");
			}

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
				NailSetupUtil.ReplaceNailMaterial(handsNailObjects, leftFootNailObjects, rightFootNailObjects, this.NailDesignAndVariationNames, this.NailShapeName, this.GenerateMaterial, false, this.OverrideMaterial,
					this.EnableAdditionalMaterials, this.PerFingerAdditionalMaterials);
			} catch (Exception) {
				Undo.RevertAllInCurrentGroup();
				throw;
			}

			try {
				NailSetupUtil.AttachAdditionalObjects(handsNailObjects, this.NailDesignAndVariationNames, this.NailShapeName, false, this.PerFingerAdditionalObjects);
			} catch (Exception) {
				Undo.RevertAllInCurrentGroup();
				throw;
			}

			// 足の追加オブジェクト (per-finger indices 10-19)
			if (this.UseFootNail && this.PerFingerAdditionalObjects != null)
			{
				try {
					Transform?[] footNailObjects = leftFootNailObjects.Concat(rightFootNailObjects).ToArray();
					for (int fi = 0; fi < footNailObjects.Length; fi++)
					{
						int perFingerIdx = fi + 10;
						if (perFingerIdx >= this.PerFingerAdditionalObjects.Length) continue;
						var fingerObjects = this.PerFingerAdditionalObjects[perFingerIdx];
						if (fingerObjects == null) continue;
						// 親付け先がない場合は Instantiate 済み孤児 GO を Destroy する (Scene 残留防止).
						if (footNailObjects[fi] == null) {
							foreach (Transform additionalObject in fingerObjects) {
								if (additionalObject != null) Object.DestroyImmediate(additionalObject.gameObject);
							}
							continue;
						}
						foreach (Transform additionalObject in fingerObjects)
							additionalObject.SetParent(footNailObjects[fi], false);
					}
				} catch (Exception) {
					Undo.RevertAllInCurrentGroup();
					throw;
				}
			}

			// Mip Streaming有効化
			try {
				var allRenderers = handsNailObjects
					.Concat(this.UseFootNail
						? leftFootNailObjects.Concat(rightFootNailObjects)
						: Enumerable.Empty<Transform?>())
					.Where(t => t != null)
					.SelectMany(t => t!.GetComponentsInChildren<Renderer>(true))
					.Cast<Renderer?>();
				NailSetupUtil.EnableMipStreamingForRenderers(allRenderers);
			} catch (Exception e) {
				ToolConsole.Log($"[Warning] {(LanguageManager.CurrentLanguageData.language == "ja" ? "Mip Streamingの有効化に失敗しました" : "Failed to enable Mip Streaming")}: {e.Message}{BuildDiagnosticInfo()}");
			}

			// ---- BlendShapeのベイクとMA同期設定 ----
			// AvatarVariationにblendShapeSyncSourcesが設定されている場合、ネイルメッシュにブレンドシェイプをコピーする
			string[]? blendShapeSyncSources = this.AvatarVariationData.BlendShapeSyncSources;
			List<(SkinnedMeshRenderer sourceSmr, string sourcePath)> resolvedSourceSmrs = new();
			if (blendShapeSyncSources != null && blendShapeSyncSources.Length > 0) {
				foreach (string sourcePath in blendShapeSyncSources) {
					// Step 1: パスで検索
					Transform? sourceTransform = this.Avatar.transform.Find(sourcePath);
					// Step 2: 名前完全一致で検索
					if (sourceTransform == null) {
						sourceTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
							.FirstOrDefault(t => t.name == System.IO.Path.GetFileName(sourcePath));
					}
					// Step 3: 名前の大文字小文字を無視して検索
					if (sourceTransform == null) {
						string sourceName = System.IO.Path.GetFileName(sourcePath);
						sourceTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
							.FirstOrDefault(t => string.Equals(t.name, sourceName, System.StringComparison.OrdinalIgnoreCase));
					}
					if (sourceTransform == null) {
						ToolConsole.Log($"[Warning] {(LanguageManager.CurrentLanguageData.language == "ja" ? "BlendShape同期元のメッシュが見つかりません" : "BlendShape sync source mesh not found")}: '{sourcePath}'{BuildDiagnosticInfo()}");
						continue;
					}
					SkinnedMeshRenderer? sourceSmr = sourceTransform.GetComponent<SkinnedMeshRenderer>();
					if (sourceSmr == null || sourceSmr.sharedMesh == null) {
						ToolConsole.Log($"[Warning] {(LanguageManager.CurrentLanguageData.language == "ja" ? "BlendShape同期元にメッシュデータがありません" : "BlendShape sync source has no mesh data")}: '{sourcePath}'{BuildDiagnosticInfo()}");
						continue;
					}
					resolvedSourceSmrs.Add((sourceSmr, sourcePath));
				}

				// フォールバック: 指定された名前で見つからない場合、
				// VisemeSkinnedMesh以外でBlendShapeを持つSmRから体メッシュを推測する
				if (resolvedSourceSmrs.Count == 0) {
					SkinnedMeshRenderer? visemeSmr = this.Avatar.VisemeSkinnedMesh;
					var fallbackCandidate = this.Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
						.Where(smr => smr != visemeSmr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
						.OrderByDescending(smr => smr.sharedMesh!.blendShapeCount)
						.FirstOrDefault();
					if (fallbackCandidate != null) {
						resolvedSourceSmrs.Add((fallbackCandidate, fallbackCandidate.gameObject.name));
						ToolConsole.Log($"{(LanguageManager.CurrentLanguageData.language == "ja" ? $"BlendShapeSyncSource フォールバック: '{fallbackCandidate.gameObject.name}' を使用します" : $"BlendShapeSyncSource fallback: using '{fallbackCandidate.gameObject.name}'")}");

					}
				}

				if (resolvedSourceSmrs.Count > 0) {
					string bakeBasePath = $"{MDNailToolDefines.GENERATED_ASSET_PATH}BlendShapeMesh/{this.AvatarName}/{this.AvatarVariationData.VariationName}";
					// UseFootNail=falseの場合は足ネイルをベイク対象に含めない（後でDestroyされるため）
					var allNailObjects = this.UseFootNail
						? handsNailObjects.Concat(leftFootNailObjects).Concat(rightFootNailObjects)
						: (IEnumerable<Transform?>)handsNailObjects;
					try {
						NailSetupUtil.BakeBlendShapesToNails(
							allNailObjects,
							resolvedSourceSmrs.Select(x => x.sourceSmr),
							bakeBasePath,
							this.AvatarVariationData.BlendShapeInitialWeights);
					} catch (Exception e) {
						ToolConsole.Log($"[Warning] {(LanguageManager.CurrentLanguageData.language == "ja" ? "BlendShapeのベイクに失敗しました" : "Failed to bake BlendShapes")}: {e.Message}{BuildDiagnosticInfo()}");
					}
				}
			}

			// ---- ネイルSMRのlocalBoundsを広めに固定(フラスタムカリング・最適化対策)----
			// MA MeshSettings 未適用時のフォールバック。Bounds ベースの可視性判定にも対応する。
			ApplyNailBoundsGuard(handsNailObjects);
			if (this.UseFootNail) {
				ApplyNailBoundsGuard(leftFootNailObjects);
				ApplyNailBoundsGuard(rightFootNailObjects);
			}

			// scale退避→配置→復元。歪み防止
			Dictionary<Transform, Vector3> savedBoneScales = new();
			Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>? corrections = null;
			if (this.ArmatureScaleCompensation)
			{
				savedBoneScales = SaveAndNeutralizeBoneScales(this.Avatar);

				var allNails = new List<Transform?>();
				var allBoneIndices = new List<int>();
				int ci = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
				foreach (Transform? nail in handsNailObjects)
				{
					ci++;
					allNails.Add(nail);
					allBoneIndices.Add(ci);
				}
				if (this.UseFootNail)
				{
					ci = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
					foreach (Transform? nail in leftFootNailObjects)
					{
						ci++;
						allNails.Add(nail);
						allBoneIndices.Add(ci);
					}
					ci = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
					foreach (Transform? nail in rightFootNailObjects)
					{
						ci++;
						allNails.Add(nail);
						allBoneIndices.Add(ci);
					}
				}
				corrections = ComputeScaleCompensatedTransforms(
					this.Avatar, targetBoneDictionary,
					allNails.ToArray(), allBoneIndices.ToArray());
			}
			try
			{
				if (this.ForModularAvatar) {
					SetupForModularAvatar(nailPrefabObject, targetBoneDictionary, handsNailObjects,
						leftFootNailObjects, rightFootNailObjects, resolvedSourceSmrs, corrections);
				} else {
					SetupDirect(nailPrefabObject, targetBoneDictionary, handsNailObjects,
						leftFootNailObjects, rightFootNailObjects, corrections);
				}
			}
			finally
			{
				RestoreBoneScales(savedBoneScales);
			}
		}

		// rootとHumanoidボーンのscaleを退避して1に揃える
		private static Dictionary<Transform, Vector3> SaveAndNeutralizeBoneScales(VRCAvatarDescriptor avatar)
		{
			var saved = new Dictionary<Transform, Vector3>();
			if (avatar == null) return saved;

			if (avatar.transform.localScale != Vector3.one)
			{
				Undo.RecordObject(avatar.transform, "Nail Setup Scale Neutralize");
				saved[avatar.transform] = avatar.transform.localScale;
				avatar.transform.localScale = Vector3.one;
			}

			Animator anim = avatar.GetComponent<Animator>();
			if (anim == null || anim.avatar == null || !anim.avatar.isHuman) return saved;

			foreach (HumanBodyBones hbb in System.Enum.GetValues(typeof(HumanBodyBones)))
			{
				if (hbb == HumanBodyBones.LastBone) continue;
				Transform? t = anim.GetBoneTransform(hbb);
				if (t == null) continue;
				if (t.localScale != Vector3.one)
				{
					Undo.RecordObject(t, "Nail Setup Scale Neutralize");
					saved[t] = t.localScale;
					t.localScale = Vector3.one;
				}
			}
			return saved;
		}

		// scale復元
		private static void RestoreBoneScales(Dictionary<Transform, Vector3> saved)
		{
			if (saved == null) return;
			foreach (var kv in saved)
			{
				if (kv.Key != null) kv.Key.localScale = kv.Value;
			}
		}

		// 表示範囲を広めに固定。カリング対策
		private static void ApplyNailBoundsGuard(Transform?[] nailObjects) {
			var guardBounds = new Bounds(Vector3.zero, Vector3.one);
			foreach (Transform? nailObject in nailObjects) {
				if (nailObject == null) continue;
				SkinnedMeshRenderer? smr = nailObject.GetComponent<SkinnedMeshRenderer>();
				if (smr == null) continue;
				smr.localBounds = guardBounds;
			}
		}

		private void SetupForModularAvatar(
			GameObject nailPrefabObject,
			Dictionary<string, Transform?> targetBoneDictionary,
			Transform?[] handsNailObjects,
			Transform?[] leftFootNailObjects,
			Transform?[] rightFootNailObjects,
			List<(SkinnedMeshRenderer sourceSmr, string sourcePath)> resolvedSourceSmrs,
			Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>? corrections) {
#if MD_NAIL_FOR_MA
				string variationName = this.AvatarVariationData.VariationName;
				string handWrapperName = $"HandNail_{variationName}";
				string footWrapperName = $"FootNail_{variationName}";

				// ---- BakeBlendShapes=trueの場合: 先にボーン位置に配置してからCombine ----
				GameObject? handCombinedGo = null;
				GameObject? footCombinedGo = null;
				if (this.BakeBlendShapes)
				{
					string bsPath = $"{MDNailToolDefines.GENERATED_ASSET_PATH}CombinedMesh/{this.AvatarName}";
					
					// ターゲットボーンへの親設定
					int bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
					foreach (Transform? nailObject in handsNailObjects)
					{
						bpIndex++;
						if (nailObject == null) continue;
						Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
						if (targetBone == null) continue;
						Vector3? desired = null;
						if (corrections != null && corrections.TryGetValue(nailObject, out var c))
						{
							nailObject.position = c.position;
							nailObject.rotation = c.rotation;
							desired = c.desiredLossyScale;
						}
						nailObject.SetParent(targetBone, true);
					}
					if (this.UseFootNail)
					{
						bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
						foreach (Transform? nailObject in leftFootNailObjects)
						{
							bpIndex++;
							if (nailObject == null) continue;
							Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
							if (targetBone == null) continue;
							Vector3? desired = null;
							if (corrections != null && corrections.TryGetValue(nailObject, out var c))
							{
								nailObject.position = c.position;
								nailObject.rotation = c.rotation;
								desired = c.desiredLossyScale;
							}
							nailObject.SetParent(targetBone, true);
							}
						bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
						foreach (Transform? nailObject in rightFootNailObjects)
						{
							bpIndex++;
							if (nailObject == null) continue;
							Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
							if (targetBone == null) continue;
							Vector3? desired = null;
							if (corrections != null && corrections.TryGetValue(nailObject, out var c))
							{
								nailObject.position = c.position;
								nailObject.rotation = c.rotation;
								desired = c.desiredLossyScale;
							}
							nailObject.SetParent(targetBone, true);
							}
					}
					else
					{
						foreach (Transform? nailObject in leftFootNailObjects)
							if (nailObject != null) Object.DestroyImmediate(nailObject.gameObject);
						foreach (Transform? nailObject in rightFootNailObjects)
							if (nailObject != null) Object.DestroyImmediate(nailObject.gameObject);
					}
					AssetDatabase.SaveAssets();

					// バリアントの構築
					List<(string Name, Transform?[] VariantNails, string? LeftName, string? RightName)> handVariants = new();
					List<(string Name, Transform?[] VariantNails, string? LeftName, string? RightName)> footVariants = new();
					List<GameObject> objectsToDestroy = new();

					AvatarBlendShapeVariant[]? activeVariants = this.AvatarVariationData.BlendShapeVariants ?? this.AvatarEntity?.BlendShapeVariants;
					if (activeVariants != null)
					{
						// バリアント解決にはアセットのインポートが必要な場合があるため、
						// StartAssetEditingのバッチモードを一時中断する
						AssetDatabase.StopAssetEditing();
						try
						{
						foreach (AvatarBlendShapeVariant variant in activeVariants)
						{
							string? variantPath = ResolveVariantPath(variant);
							if (string.IsNullOrEmpty(variantPath))
							{
								if (!string.IsNullOrEmpty(variant.NailPrefabGUID))
								{
									string msg = LanguageManager.CurrentLanguageData.language == "ja"
									? $"Variant '{variant.Name}': GUID={variant.NailPrefabGUID} のパスが見つかりません"
									: $"Variant '{variant.Name}': path not found for GUID={variant.NailPrefabGUID}";
									msg += BuildDiagnosticInfo(includeFolder: true);
									ToolConsole.Log($"[Warning] {msg}");
									this.Warnings.Add(msg);
								}
								continue;
							}
							AssetDatabase.ImportAsset(variantPath, ImportAssetOptions.ForceSynchronousImport);
							GameObject? variantPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
							if (variantPrefabAsset == null)
							{
								string msg = LanguageManager.CurrentLanguageData.language == "ja"
								? $"Variant '{variant.Name}': プレハブの読み込みに失敗しました (path={variantPath})"
								: $"Variant '{variant.Name}': failed to load prefab (path={variantPath})";
								msg += BuildDiagnosticInfo(includeFolder: true);
								ToolConsole.Log($"[Warning] {msg}");
								this.Warnings.Add(msg);
								continue;
							}

							GameObject resolvedVariantPrefab = ResolveShapePrefab(variantPrefabAsset, this.NailShapeName);
							GameObject instVariant = Object.Instantiate(resolvedVariantPrefab, this.Avatar.transform);
							// C-1 fix: instVariant の objectsToDestroy 追加は全 vNail.SetParent 完了後に移動.
							// (L510 -> ループ末尾) 詳細は variants ループ末尾参照.

							// バリアントプレハブの子名からシェイプ接頭辞([Oval]等)を除去
							{
								Regex varPrefixRegex = new(@"(?<prefix>\[.+\]).+");
								Match varPrefixMatch = varPrefixRegex.Match(resolvedVariantPrefab.name);
								if (varPrefixMatch.Success)
								{
									string varPrefix = varPrefixMatch.Groups["prefix"].Value;
									foreach (Transform child in instVariant.transform)
										child.name = child.name.Replace(varPrefix, "");
								}
							}

							Transform?[] varHands = GetHandsNailObjectList(instVariant);
							Transform?[] varLeftFoot = GetLeftFootNailObjectList(instVariant);
							Transform?[] varRightFoot = GetRightFootNailObjectList(instVariant);

							// バリアントネイルのメッシュが未解決(null)の場合、ベースネイルのメッシュをコピー
							// (プレハブが参照するFBXが存在しない場合の対策)
							CopyMeshIfNull(varHands, handsNailObjects);
							CopyMeshIfNull(varLeftFoot, leftFootNailObjects);
							CopyMeshIfNull(varRightFoot, rightFootNailObjects);

							// Armatureスケール補正をバリアントネイルにも適用
							Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>? variantCorrections = null;
							if (corrections != null)
							{
								var varAllNails = new List<Transform?>();
								var varAllBoneIndices = new List<int>();
								int vci = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
								foreach (Transform? vn in varHands)
								{
									vci++;
									varAllNails.Add(vn);
									varAllBoneIndices.Add(vci);
								}
								if (this.UseFootNail)
								{
									vci = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
									foreach (Transform? vn in varLeftFoot)
									{
										vci++;
										varAllNails.Add(vn);
										varAllBoneIndices.Add(vci);
									}
									vci = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
									foreach (Transform? vn in varRightFoot)
									{
										vci++;
										varAllNails.Add(vn);
										varAllBoneIndices.Add(vci);
									}
								}
								variantCorrections = ComputeScaleCompensatedTransforms(
									this.Avatar, targetBoneDictionary,
									varAllNails.ToArray(), varAllBoneIndices.ToArray());
							}

							// バリアントネイルも実際の指ボーンの子にして、ローカル座標差分を計算できるようにする
							bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
							foreach (Transform? vNail in varHands)
							{
								bpIndex++;
								if (vNail == null) continue;
								objectsToDestroy.Add(vNail.gameObject);
								Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
								if (targetBone == null) continue;
								Vector3? desired = null;
								if (variantCorrections != null && variantCorrections.TryGetValue(vNail, out var vc))
								{
									vNail.position = vc.position;
									vNail.rotation = vc.rotation;
									desired = vc.desiredLossyScale;
								}
								vNail.SetParent(targetBone, true);
									}
							if (varHands.Any(t => t != null))
								handVariants.Add((variant.Name, varHands, variant.LeftBlendShapeName, variant.RightBlendShapeName));

							if (this.UseFootNail)
							{
								bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
								foreach (Transform? vNail in varLeftFoot)
								{
									bpIndex++;
									if (vNail == null) continue;
									objectsToDestroy.Add(vNail.gameObject);
									Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
									if (targetBone == null) continue;
									Vector3? desired = null;
									if (variantCorrections != null && variantCorrections.TryGetValue(vNail, out var vc))
									{
										vNail.position = vc.position;
										vNail.rotation = vc.rotation;
										desired = vc.desiredLossyScale;
									}
									vNail.SetParent(targetBone, true);
											}
								bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
								foreach (Transform? vNail in varRightFoot)
								{
									bpIndex++;
									if (vNail == null) continue;
									objectsToDestroy.Add(vNail.gameObject);
									Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
									if (targetBone == null) continue;
									Vector3? desired = null;
									if (variantCorrections != null && variantCorrections.TryGetValue(vNail, out var vc))
									{
										vNail.position = vc.position;
										vNail.rotation = vc.rotation;
										desired = vc.desiredLossyScale;
									}
									vNail.SetParent(targetBone, true);
											}
								var varFeetAll = varLeftFoot.Concat(varRightFoot).ToArray();
								if (varFeetAll.Any(t => t != null))
									footVariants.Add((variant.Name, varFeetAll, variant.LeftBlendShapeName, variant.RightBlendShapeName));

								// C-1 fix: 全 vNail が SetParent 完了したのでルート GO を Destroy 対象に追加.
								// 子 vNail は別途 objectsToDestroy 追加済み. null check により二重 Destroy は安全.
								objectsToDestroy.Add(instVariant);
							}
						}
						}
						finally
						{
							AssetDatabase.StartAssetEditing();
						}
					}

					// 追加オブジェクトの退避（BakeAndCombineがネイルオブジェクトを破棄するため）
					var preservedAdditionalObjects = new List<(Transform additionalObj, Transform targetBone)>();
					foreach (Transform? nailObject in handsNailObjects)
					{
						if (nailObject == null) continue;
						Transform? targetBone = nailObject.parent;
						if (targetBone == null) continue;
						foreach (Transform child in nailObject.Cast<Transform>().ToArray())
						{
							child.SetParent(null, true);
							preservedAdditionalObjects.Add((child, targetBone));
						}
					}

					// 破綻防止: 体めり込み補正用のボディSMRを探す
					SkinnedMeshRenderer? bodySmrForPushOut = null;
					if (this.EnablePenetrationCorrection && activeVariants != null && activeVariants.Length > 1)
					{
						string? syncSmrName = activeVariants
							.Select(v => v.SyncSourceSmrName)
							.FirstOrDefault(n => !string.IsNullOrEmpty(n));
						if (!string.IsNullOrEmpty(syncSmrName))
						{
							Transform? bodyTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
								.FirstOrDefault(t => t.name == syncSmrName);
							if (bodyTransform == null)
								bodyTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
									.FirstOrDefault(t => string.Equals(t.name, syncSmrName, System.StringComparison.OrdinalIgnoreCase));
							if (bodyTransform != null)
								bodySmrForPushOut = bodyTransform.GetComponent<SkinnedMeshRenderer>();
						}
						if (bodySmrForPushOut == null)
						{
							SkinnedMeshRenderer? visemeSmr = this.Avatar.VisemeSkinnedMesh;
							bodySmrForPushOut = this.Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
								.Where(smr => smr != visemeSmr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
								.OrderByDescending(smr => smr.sharedMesh!.blendShapeCount)
								.FirstOrDefault();
						}

					}

					// メッシュ統合
					bool[] handsIsLeft = handsNailObjects.Select((_, i) => i < 5).ToArray();
					handCombinedGo = NailSetupUtil.BakeAndCombineNailMeshes(
						handsNailObjects, nailPrefabObject, handWrapperName, bsPath,
						handVariants.Count > 0 ? handVariants.ToArray() : null,
						handsIsLeft,
						bodySmrForPushOut);

					if (this.UseFootNail)
					{
						bool[] feetIsLeft = leftFootNailObjects.Select(_ => true)
							.Concat(rightFootNailObjects.Select(_ => false)).ToArray();
						footCombinedGo = NailSetupUtil.BakeAndCombineNailMeshes(
							leftFootNailObjects.Concat(rightFootNailObjects).ToArray(),
							nailPrefabObject, footWrapperName, bsPath,
							footVariants.Count > 0 ? footVariants.ToArray() : null,
							feetIsLeft,
							bodySmrForPushOut);
					}

					// 退避した追加オブジェクトを統合ラッパーに復元（BoneProxy付き）
					if (preservedAdditionalObjects.Count > 0 && handCombinedGo != null)
					{
						foreach (var (additionalObj, targetBone) in preservedAdditionalObjects)
						{
							if (additionalObj == null) continue;
							additionalObj.SetParent(handCombinedGo.transform, true);
							ModularAvatarBoneProxy bp = additionalObj.gameObject.AddComponent<ModularAvatarBoneProxy>();
							bp.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
							bp.target = targetBone;
						}
					}

					// バリアントインスタンスの破棄
					foreach (GameObject obj in objectsToDestroy)
					{
						if (obj != null) Object.DestroyImmediate(obj);
					}
				}

				GameObject handWrapper;
				GameObject? footWrapper = null;

				if (this.BakeBlendShapes && handCombinedGo != null)
				{
					handWrapper = handCombinedGo;
				}
				else
				{
					// ---- HandNailラッパー作成 ----
					handWrapper = new GameObject(handWrapperName);
					Undo.RegisterCreatedObjectUndo(handWrapper, "Nail Setup");
					handWrapper.transform.SetParent(nailPrefabObject.transform, false);
					foreach (Transform? nailObject in handsNailObjects)
					{
						if (nailObject == null) continue;
						nailObject.SetParent(handWrapper.transform, false);
					}
				}

				if (this.UseFootNail)
				{
					if (this.BakeBlendShapes && footCombinedGo != null)
					{
						footWrapper = footCombinedGo;
					}
					else
					{
						// ---- FootNailラッパー作成 ----
						footWrapper = new GameObject(footWrapperName);
						Undo.RegisterCreatedObjectUndo(footWrapper, "Nail Setup");
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
				}
				else if (!this.BakeBlendShapes)
				{
					foreach (Transform? nailObject in leftFootNailObjects)
						if (nailObject != null) Object.DestroyImmediate(nailObject.gameObject);
					foreach (Transform? nailObject in rightFootNailObjects)
						if (nailObject != null) Object.DestroyImmediate(nailObject.gameObject);
				}

				// ---- BoneProxy設定 (BakeBlendShapes=falseの場合のみ) ----
				if (!this.BakeBlendShapes)
				{
					int index = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
					foreach (Transform? nailObject in handsNailObjects)
					{
						index++;
						if (nailObject == null) continue;
						Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
						if (corrections != null && corrections.TryGetValue(nailObject, out var c))
						{
							nailObject.position = c.position;
							nailObject.rotation = c.rotation;
						}
						if (targetBone == null) continue;
						nailObject.SetParent(targetBone, true);
						ModularAvatarBoneProxy boneProxy = nailObject.gameObject.AddComponent<ModularAvatarBoneProxy>();
						boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
						boneProxy.target = targetBone;
					}

					if (this.UseFootNail)
					{
						index = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
						foreach (Transform? nailObject in leftFootNailObjects)
						{
							index++;
							if (nailObject == null) continue;
							Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
							if (corrections != null && corrections.TryGetValue(nailObject, out var c))
							{
								nailObject.position = c.position;
								nailObject.rotation = c.rotation;
								}
							if (targetBone == null) continue;
							nailObject.SetParent(targetBone, true);
							ModularAvatarBoneProxy boneProxy = nailObject.gameObject.AddComponent<ModularAvatarBoneProxy>();
							boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
							boneProxy.target = targetBone;
						}
						index = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
						foreach (Transform? nailObject in rightFootNailObjects)
						{
							index++;
							if (nailObject == null) continue;
							Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
							if (corrections != null && corrections.TryGetValue(nailObject, out var c))
							{
								nailObject.position = c.position;
								nailObject.rotation = c.rotation;
								}
							if (targetBone == null) continue;
							nailObject.SetParent(targetBone, true);
							ModularAvatarBoneProxy boneProxy = nailObject.gameObject.AddComponent<ModularAvatarBoneProxy>();
							boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
							boneProxy.target = targetBone;
						}
					}
				}

				// ---- BlendShapeSync設定（ブレンドシェイプが同期元として設定されている場合）----
				if (!this.BakeBlendShapes && resolvedSourceSmrs.Count > 0)
				{
					var allNailObjectsForSync = handsNailObjects
						.Concat(this.UseFootNail ? (IEnumerable<Transform?>)leftFootNailObjects.Concat(rightFootNailObjects) : Enumerable.Empty<Transform?>());
					foreach (Transform? nailObject in allNailObjectsForSync)
					{
						if (nailObject == null) continue;
						SkinnedMeshRenderer? nailSmr = nailObject.GetComponent<SkinnedMeshRenderer>();
						if (nailSmr == null || nailSmr.sharedMesh == null) continue;

						var bindings = new List<BlendshapeBinding>();
						foreach ((SkinnedMeshRenderer sourceSmr, string _) in resolvedSourceSmrs)
						{
							Mesh? sourceMesh = sourceSmr.sharedMesh;
							if (sourceMesh == null) continue;
							for (int si = 0; si < sourceMesh.blendShapeCount; si++)
							{
								string shapeName = sourceMesh.GetBlendShapeName(si);
								if (nailSmr.sharedMesh.GetBlendShapeIndex(shapeName) < 0) continue;
								bindings.Add(new BlendshapeBinding
								{
									ReferenceMesh = CreateAvatarRef(sourceSmr.gameObject),
									Blendshape = shapeName,
									LocalBlendshape = shapeName
								});
							}
						}
						if (bindings.Count > 0)
						{
							ModularAvatarBlendshapeSync bsSync = nailObject.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
							bsSync.Bindings = bindings;
						}
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
						// A-1 fix: 既存ルート内の旧 HandNail/FootNail ラッパーを先に破棄してから新wrapperを統合する.
						// 色違い再Apply時に旧wrapperが残ってネイル多重積みになるバグの修正.
						// handWrapperName/footWrapperName は SetupForModularAvatar 冒頭 (L398-399) で既に宣言済み.
						Transform? oldHand = existingNailRoot.Find(handWrapperName);
						Transform? oldFoot = existingNailRoot.Find(footWrapperName);
						if (oldHand != null) Undo.DestroyObjectImmediate(oldHand.gameObject);
						if (oldFoot != null) Undo.DestroyObjectImmediate(oldFoot.gameObject);

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
						// A-1 fix: MergeAnLabo=false ルートでも同様に旧wrapperを破棄してから統合する.
						// handWrapperName/footWrapperName は SetupForModularAvatar 冒頭 (L398-399) で既に宣言済み.
						Transform? oldHand = existingRoot.Find(handWrapperName);
						Transform? oldFoot = existingRoot.Find(footWrapperName);
						if (oldHand != null) Undo.DestroyObjectImmediate(oldHand.gameObject);
						if (oldFoot != null) Undo.DestroyObjectImmediate(oldFoot.gameObject);

						handWrapper.transform.SetParent(existingRoot, false);
						if (footWrapper != null) footWrapper.transform.SetParent(existingRoot, false);
						Object.DestroyImmediate(nailPrefabObject);
						nailPrefabObject = existingRoot.gameObject;
					}
				}

				// ---- マーカーコンポーネント追加 (重複付与防止) ----
				// B-1 fix: AddComponent の結果を Undo に登録. Undo時にMarkerだけ消えてGOが残るデグレを防ぐ.
				if (nailPrefabObject.GetComponent<MDNailObjectMarker>() == null)
				{
					MDNailObjectMarker marker = nailPrefabObject.AddComponent<MDNailObjectMarker>();
					Undo.RegisterCreatedObjectUndo(marker, "Nail Setup Marker");
				}

				// ---- MA Mesh Settings（バウンディングボックスによるカリング防止）----
				if (nailPrefabObject.GetComponent<ModularAvatarMeshSettings>() == null)
				{
					ModularAvatarMeshSettings meshSettings = nailPrefabObject.AddComponent<ModularAvatarMeshSettings>();
					meshSettings.InheritProbeAnchor = ModularAvatarMeshSettings.InheritMode.SetOrInherit;
					meshSettings.InheritBounds = ModularAvatarMeshSettings.InheritMode.SetOrInherit;

					Animator? animator = this.Avatar.GetComponent<Animator>();
					if (animator != null)
					{
						Transform? chest = animator.GetBoneTransform(HumanBodyBones.Chest);
						if (chest != null)
							meshSettings.ProbeAnchor = CreateAvatarRef(chest.gameObject);

						Transform? hips = animator.GetBoneTransform(HumanBodyBones.Hips);
						if (hips != null)
							meshSettings.RootBone = CreateAvatarRef(hips.gameObject);
					}

					meshSettings.Bounds = new Bounds(Vector3.zero, Vector3.one * 2);
				}

				// ---- avatar-levelブレンドシェイプバリアントのBlendShapeSync設定 ----
				// ※ nailPrefabObjectがアバター階層下に配置された後に実行する必要がある
				// （AvatarObjectReferenceがアバタールートからの相対パスを正しく解決するため）
				AvatarBlendShapeVariant[]? syncVariants = this.AvatarVariationData.BlendShapeVariants ?? this.AvatarEntity?.BlendShapeVariants;
				if (this.SyncBlendShapesWithMA && syncVariants != null)
				{
					// BakeAndCombineで生成されたCombined SMRを含む全子オブジェクトを対象
					foreach (Transform child in nailPrefabObject.transform)
					{
						SkinnedMeshRenderer? bsSmr = child.GetComponent<SkinnedMeshRenderer>();
						if (bsSmr == null || bsSmr.sharedMesh == null) continue;
						if (bsSmr.sharedMesh.blendShapeCount == 0) continue;
						var variantBindings = new List<BlendshapeBinding>();
						foreach (AvatarBlendShapeVariant variant in syncVariants)
						{
							if (string.IsNullOrEmpty(variant.SyncSourceSmrName)) continue;
							// Step 1: 名前完全一致
							Transform? srcSmrTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
								.FirstOrDefault(t => t.name == variant.SyncSourceSmrName);
							// Step 2: 大文字小文字無視
							if (srcSmrTransform == null) {
								srcSmrTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
									.FirstOrDefault(t => string.Equals(t.name, variant.SyncSourceSmrName, System.StringComparison.OrdinalIgnoreCase));
							}
							// Step 3: 部分一致（"Body" → "Body all" など）
							if (srcSmrTransform == null) {
								srcSmrTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
									.FirstOrDefault(t => t.GetComponent<SkinnedMeshRenderer>() != null
										&& (t.name.Contains(variant.SyncSourceSmrName!) || variant.SyncSourceSmrName!.Contains(t.name)));
							}
							// Step 4: BlendShapeを持つ非顔SmRから推測
							if (srcSmrTransform == null) {
								SkinnedMeshRenderer? visemeSmr = this.Avatar.VisemeSkinnedMesh;
								srcSmrTransform = this.Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
									.Where(smr => smr != visemeSmr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
									.OrderByDescending(smr => smr.sharedMesh!.blendShapeCount)
									.FirstOrDefault()?.transform;
								if (srcSmrTransform != null) {
									ToolConsole.Log($"{(LanguageManager.CurrentLanguageData.language == "ja" ? $"BlendShapeSync: '{variant.SyncSourceSmrName}' が見つからないため、フォールバック '{srcSmrTransform.name}' を使用します" : $"BlendShapeSync: '{variant.SyncSourceSmrName}' not found, using fallback '{srcSmrTransform.name}'")}");

								}
							}
							if (srcSmrTransform == null) { ToolConsole.Log($"[Warning] {(LanguageManager.CurrentLanguageData.language == "ja" ? $"BlendShape同期元のメッシュ '{variant.SyncSourceSmrName}' がアバターに見つかりません" : $"BlendShape sync source mesh '{variant.SyncSourceSmrName}' not found on avatar")}{BuildDiagnosticInfo()}"); continue; }

							if (!string.IsNullOrEmpty(variant.LeftBlendShapeName) && !string.IsNullOrEmpty(variant.RightBlendShapeName))
							{
								// L/R分割モード: 結合メッシュ上のL/R Blendshapeをそれぞれアバターのものとバインド
								AvatarObjectReference aoRef = CreateAvatarRef(srcSmrTransform.gameObject);
								foreach (string lrName in new[] { variant.LeftBlendShapeName!, variant.RightBlendShapeName! })
								{
									string actualLRName = lrName;
									string normalizedLRName = lrName.Replace(" ", "").Replace("　", "");
									for (int shi = 0; shi < bsSmr.sharedMesh.blendShapeCount; shi++)
									{
										string sn = bsSmr.sharedMesh.GetBlendShapeName(shi);
										if (sn.Replace(" ", "").Replace("　", "") == normalizedLRName)
										{
											actualLRName = sn;
											break;
										}
									}
									if (bsSmr.sharedMesh.GetBlendShapeIndex(actualLRName) < 0) continue;
									variantBindings.Add(new BlendshapeBinding
									{
										ReferenceMesh = aoRef,
										Blendshape = actualLRName,
										LocalBlendshape = actualLRName
									});
								}
							}
							else
							{
								// 既存動作（L/R未設定時）
								string actualShapeName = variant.Name;
								string normalizedVariantName = variant.Name.Replace(" ", "").Replace("　", "");
								for (int shi = 0; shi < bsSmr.sharedMesh.blendShapeCount; shi++)
								{
									string sn = bsSmr.sharedMesh.GetBlendShapeName(shi);
									if (sn.Replace(" ", "").Replace("　", "") == normalizedVariantName)
									{
										actualShapeName = sn;
										break;
									}
								}

								int bsIndex = bsSmr.sharedMesh.GetBlendShapeIndex(actualShapeName);
								if (bsIndex < 0) continue;

								AvatarObjectReference aoRef = CreateAvatarRef(srcSmrTransform.gameObject);
								variantBindings.Add(new BlendshapeBinding
								{
									ReferenceMesh = aoRef,
									Blendshape = actualShapeName,
									LocalBlendshape = actualShapeName
								});
							}
						}
						if (variantBindings.Count > 0)
						{
							ModularAvatarBlendshapeSync variantBsSync = child.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
							variantBsSync.Bindings = variantBindings;
						}
					}
				}

				if (this.GenerateExpressionMenu)
					this.SetupExpressionMenu(nailPrefabObject);
#else
				Undo.RevertAllInCurrentGroup();
				throw new InvalidOperationException("The setup for ModularAvatar cannot be executed in environments where ModularAvatar is not installed.");
#endif
		}

		private void SetupDirect(
			GameObject nailPrefabObject,
			Dictionary<string, Transform?> targetBoneDictionary,
			Transform?[] handsNailObjects,
			Transform?[] leftFootNailObjects,
			Transform?[] rightFootNailObjects,
			Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>? corrections) {
				int index = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
				foreach (Transform? nailObject in handsNailObjects) {
					index++;
					if (nailObject == null) continue;
					Transform? target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
					if (target == null) {
						ToolConsole.Log($"[Error] Not found target bone : {MDNailToolDefines.TARGET_BONE_NAME_LIST[index]}");
						continue;
					}

					Vector3? desired = null;
					if (corrections != null && corrections.TryGetValue(nailObject, out var c)) {
						nailObject.position = c.position;
						nailObject.rotation = c.rotation;
						desired = c.desiredLossyScale;
					}
					nailObject.SetParent(target, true);
				}

				if (this.UseFootNail) {
					index = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
					foreach (Transform? nailObject in leftFootNailObjects) {
						index++;
						if (nailObject == null) continue;
						Transform? target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
						if (target == null) {
							ToolConsole.Log($"[Error] Not found target bone : {MDNailToolDefines.TARGET_BONE_NAME_LIST[index]}");
							continue;
						}

						Vector3? desired = null;
						if (corrections != null && corrections.TryGetValue(nailObject, out var c)) {
							nailObject.position = c.position;
							nailObject.rotation = c.rotation;
							desired = c.desiredLossyScale;
						}
						nailObject.SetParent(target, true);
					}

					foreach (Transform? nailObject in rightFootNailObjects) {
						index++;
						if (nailObject == null) continue;
						Transform? target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
						if (target == null) {
							ToolConsole.Log($"[Error] Not found target bone : {MDNailToolDefines.TARGET_BONE_NAME_LIST[index]}");
							continue;
						}

						Vector3? desired = null;
						if (corrections != null && corrections.TryGetValue(nailObject, out var c)) {
							nailObject.position = c.position;
							nailObject.rotation = c.rotation;
							desired = c.desiredLossyScale;
						}
						nailObject.SetParent(target, true);
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

		// Unity AssetDatabase は ø / é / 絵文字 など一部の非ASCII文字を含む .meta 書き込みに失敗する
		// [例: "Phenøa" -> Cannot open file ... for write]
		// a-zA-Z0-9_- とひらがな・カタカナ・漢字のみ通し、それ以外は _ に置換する。
		private static string SanitizeForFileName(string name) {
			if (string.IsNullOrEmpty(name)) return "avatar";
			string sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_\-぀-ゟ゠-ヿ一-鿿]", "_");
			return string.IsNullOrEmpty(sanitized) ? "avatar" : sanitized;
		}

		// SaveAsPrefabAsset は Native 内部で .meta 二重書込エラーを出すため避ける
		public static void CreateBackup(GameObject avatarGameObject) {
			if (!Directory.Exists(MDNailToolDefines.BACKUP_PATH)) {
				Directory.CreateDirectory(MDNailToolDefines.BACKUP_PATH);
				AssetDatabase.Refresh();
			}

			string safeAvatarName = SanitizeForFileName(avatarGameObject.name);
			string sceneName = $"bk_{safeAvatarName}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.unity";
			string destPath = MDNailToolDefines.BACKUP_PATH + sceneName;

			Scene previousActive = SceneManager.GetActiveScene();
			Scene backupScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
			try {
				GameObject clonedObject = Object.Instantiate(avatarGameObject);
				SceneManager.MoveGameObjectToScene(clonedObject, backupScene);
				EditorSceneManager.SaveScene(backupScene, destPath);
			} finally {
				EditorSceneManager.CloseScene(backupScene, removeScene: true);
				if (previousActive.IsValid()) {
					EditorSceneManager.SetActiveScene(previousActive);
				}
			}
		}


		private string getPrefabPrefix() {
			Regex regex = new(@"(?<prefix>\[.+\]).+");
			Match match = regex.Match(this.NailPrefab.name);
			if (match.Success) return match.Groups["prefix"].Value;

			ToolConsole.Log($"[Error] Failed to obtain nail prefix. ({this.NailPrefab?.name ?? "(null)"})");
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

		internal static Dictionary<string, Transform?> GetTargetBoneDictionary(VRCAvatarDescriptor avatar, IReadOnlyDictionary<string, string>? boneMappingOverride) {
			Animator? avatarAnimator = avatar.GetComponent<Animator>();
			UEAvatar? animatorAvatar = avatarAnimator.avatar;
			HumanDescription humanDescription = animatorAvatar.humanDescription;
			HumanBone[] humanBones = humanDescription.human;
			Dictionary<string, string> boneNameDictionary = humanBones.ToDictionary(humanBone => humanBone.humanName, humanBone => humanBone.boneName);

			// HumanoidリグのHipsからアーマチュアルートを特定（ヒエラルキー順や衣装Armatureに依存しない）
			Transform? armatureRoot = null;
			Transform? hipsTransform = avatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
			if (hipsTransform != null) {
				armatureRoot = hipsTransform.parent;
				while (armatureRoot != null && armatureRoot.parent != avatar.transform) {
					armatureRoot = armatureRoot.parent;
				}
			}
			// 特定できなければアバター全体をフォールバック
			Transform searchRoot = armatureRoot != null ? armatureRoot : avatar.transform;

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

							ToolConsole.Log($"[Warning] Not found bone : {handFingerBonePath}");
						}

						return (name, searchRoot.FindRecursive(boneNameDictionary[name]));
					}


					if (boneMappingOverride != null && boneMappingOverride.TryGetValue(name, out string footFingerBonePath)) {
						// ボーンが上書きされていればそれを返す
						Transform? targetBone = avatar.transform.Find(footFingerBonePath);
						if (targetBone != null) {
							return (name, targetBone);
						}

						ToolConsole.Log($"[Warning] Not found bone : {footFingerBonePath}");
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
					return (name, searchRoot.FindRecursive(targetBoneName));
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

		// メッシュ無ければベースからコピー
		private static void CopyMeshIfNull(Transform?[] variantNails, Transform?[] baseNails) {
			int count = Math.Min(variantNails.Length, baseNails.Length);
			for (int i = 0; i < count; i++) {
				if (variantNails[i] == null || baseNails[i] == null) continue;
				SkinnedMeshRenderer? varSmr = variantNails[i]!.GetComponent<SkinnedMeshRenderer>();
				if (varSmr == null) continue;
				if (varSmr.sharedMesh != null) continue;
				SkinnedMeshRenderer? baseSmr = baseNails[i]!.GetComponent<SkinnedMeshRenderer>();
				if (baseSmr == null || baseSmr.sharedMesh == null) continue;
				varSmr.sharedMesh = baseSmr.sharedMesh;
			}
		}

		internal static GameObject ResolveShapePrefab(GameObject basePrefab, string targetShape) {
			System.Text.RegularExpressions.Regex nailPrefabNamePattern = new(@"(?<prefix>\[.+\])(?<prefabName>.+)");
			System.Text.RegularExpressions.Match match = nailPrefabNamePattern.Match(basePrefab.name);
			if (!match.Success) return basePrefab;

			string prefabName = match.Groups["prefabName"].Value;
			string prefabPath = AssetDatabase.GetAssetPath(basePrefab);
			string prefabDirPath = Path.GetDirectoryName(prefabPath) ?? "";
			GameObject current = basePrefab;
			using DBNailShape dbNailShape = new();
			foreach (NailShape nailShape in dbNailShape.collection) {
				string newPrefabPath = $"{prefabDirPath}/[{nailShape.ShapeName}]{prefabName}.prefab";
				if (File.Exists(newPrefabPath)) {
					GameObject? newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newPrefabPath);
					if (newPrefab != null) current = newPrefab;
				}
				if (nailShape.ShapeName == targetShape) break;
			}
			return current;
		}

		private static string GetRelativePath(Transform root, Transform target) {
			var parts = new List<string>();
			Transform? current = target;
			while (current != null && current != root) {
				parts.Insert(0, current.name);
				current = current.parent;
			}
			return string.Join("/", parts);
		}

		// FBX素体基準でネイルの位置と回転を計算
		internal static Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>
			ComputeScaleCompensatedTransforms(
				VRCAvatarDescriptor avatar,
				Dictionary<string, Transform?> targetBoneDictionary,
				Transform?[] nailObjects,
				int[] boneIndices)
		{
			var result = new Dictionary<Transform, (Vector3, Quaternion, Vector3)>();

			Animator? avatarAnimator = avatar.GetComponent<Animator>();
			if (avatarAnimator == null || avatarAnimator.avatar == null) return result;

			string modelPath = AssetDatabase.GetAssetPath(avatarAnimator.avatar);
			if (string.IsNullOrEmpty(modelPath)) return result;

			GameObject? referenceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
			if (referenceAsset == null) return result;

			GameObject tempInstance = Object.Instantiate(referenceAsset);
			try
			{
				tempInstance.transform.SetPositionAndRotation(avatar.transform.position, avatar.transform.rotation);
				tempInstance.transform.localScale = avatar.transform.lossyScale;

				Dictionary<string, Transform> tempBonesByName = new();
				foreach (Transform t in tempInstance.GetComponentsInChildren<Transform>())
				{
					if (!tempBonesByName.ContainsKey(t.name))
						tempBonesByName[t.name] = t;
				}

				for (int i = 0; i < nailObjects.Length; i++)
				{
					Transform? nail = nailObjects[i];
					if (nail == null) continue;
					if (boneIndices[i] < 0 || boneIndices[i] >= MDNailToolDefines.TARGET_BONE_NAME_LIST.Count) continue;

					string boneName = MDNailToolDefines.TARGET_BONE_NAME_LIST[boneIndices[i]];
					Transform? actualBone = targetBoneDictionary.GetValueOrDefault(boneName);
					if (actualBone == null) continue;

					if (!tempBonesByName.TryGetValue(actualBone.name, out Transform? tempBone)) continue;

					Vector3 localPos = tempBone.InverseTransformPoint(nail.position);
					Quaternion localRot = Quaternion.Inverse(tempBone.rotation) * nail.rotation;

					Vector3 correctedWorldPos = actualBone.TransformPoint(localPos);
					Quaternion correctedWorldRot = actualBone.rotation * localRot;

					result[nail] = (correctedWorldPos, correctedWorldRot, nail.lossyScale);
				}
			}
			finally
			{
				Object.DestroyImmediate(tempInstance);
			}

			return result;
		}

		// サイズを目標値に揃える
		internal static void EnforceLossyScale(Transform nail, Vector3 desiredLossyScale)
		{
			if (nail == null) return;
			const int MAX_ITER = 6;
			const float TOLERANCE = 0.001f;
			for (int i = 0; i < MAX_ITER; i++)
			{
				Vector3 cur = nail.lossyScale;
				float rx = Mathf.Abs(cur.x) > 1e-6f ? desiredLossyScale.x / cur.x : 1f;
				float ry = Mathf.Abs(cur.y) > 1e-6f ? desiredLossyScale.y / cur.y : 1f;
				float rz = Mathf.Abs(cur.z) > 1e-6f ? desiredLossyScale.z / cur.z : 1f;
				if (Mathf.Abs(rx - 1f) < TOLERANCE && Mathf.Abs(ry - 1f) < TOLERANCE && Mathf.Abs(rz - 1f) < TOLERANCE)
					break;
				Vector3 ls = nail.localScale;
				nail.localScale = new Vector3(ls.x * rx, ls.y * ry, ls.z * rz);
			}
		}

#if MD_NAIL_FOR_MA
	private static AvatarObjectReference CreateAvatarRef(GameObject obj) {
		// MAバージョン非依存。Set(GameObject)は全バージョンで存在し、例外を投げない
		var r = new AvatarObjectReference();
		r.Set(obj);
		return r;
	}

	private static void SetMenuSubMenu(ModularAvatarMenuItem item) {
#if MA_HAS_PORTABLE_API
		item.PortableControl.Type = PortableControlType.SubMenu;
#else
		if (item.Control == null) item.Control = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control();
		item.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.SubMenu;
#endif
	}

	private static void SetMenuToggle(ModularAvatarMenuItem item, float value = 1f) {
#if MA_HAS_PORTABLE_API
		item.PortableControl.Type = PortableControlType.Toggle;
		item.PortableControl.Value = value;
#else
		if (item.Control == null) item.Control = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control();
		item.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.Toggle;
		item.Control.value = value;
#endif
	}

	private static void SetMenuIcon(ModularAvatarMenuItem item, Texture2D? icon) {
#if MA_HAS_PORTABLE_API
		item.PortableControl.Icon = icon;
#else
		if (item.Control == null) item.Control = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control();
		item.Control.icon = icon;
#endif
	}

	private void SetupExpressionMenu(GameObject nailRoot) {
		var firstEntry = this.NailDesignAndVariationNames.FirstOrDefault(t => t.Item1 != null);
		string designName = firstEntry.Item1 != null
			? firstEntry.Item1.DesignName : (this.OverrideMaterial?.name ?? "Nail");
		string colorName = firstEntry.Item1 != null ? firstEntry.Item3 : "";
		string menuLabel = string.IsNullOrEmpty(colorName) ? designName : $"{designName}_{colorName}";
		string variationName = this.AvatarVariationData.VariationName;
		string handWrapperName = $"HandNail_{variationName}";
		string footWrapperName = $"FootNail_{variationName}";

		// サムネイル取得
		Texture2D? thumbnail = null;
		{
			using DBNailDesign db = new();
			NailDesign? design = db.FindNailDesignByDesignName(designName);
			if (design != null) {
				thumbnail = MDNailToolAssetLoader.LoadThumbnail(design.ThumbnailGUID, design.DesignName);
			}
		}

		// ---- MergeAnLabo: [An-Labo]親にMenuInstaller+SubMenuItem（まだ無ければ）----
		if (this.MergeAnLabo) {
			GameObject anLaboObj = nailRoot.transform.parent?.gameObject ?? this.Avatar.gameObject;
			if (anLaboObj.GetComponent<ModularAvatarMenuInstaller>() == null) {
				// [An-Labo]新規作成時: 最初のネイルのサムネイルを設定
				anLaboObj.AddComponent<ModularAvatarMenuInstaller>();
				var anLaboMenuItem = anLaboObj.AddComponent<ModularAvatarMenuItem>();
				SetMenuSubMenu(anLaboMenuItem);
				SetMenuIcon(anLaboMenuItem, thumbnail);
				anLaboMenuItem.label = "An-Labo";
				anLaboMenuItem.MenuSource = SubmenuSource.Children;
			} else {
				// [An-Labo]が既存（2本目以降のネイル追加時）: アイコンをnullに更新
				var existingMenuItem = anLaboObj.GetComponent<ModularAvatarMenuItem>();
				if (existingMenuItem != null)
					SetMenuIcon(existingMenuItem, null);
			}
		} else {
			// MergeAnLabo=false: nailRoot自身にMenuInstaller
			if (nailRoot.GetComponent<ModularAvatarMenuInstaller>() == null)
				nailRoot.AddComponent<ModularAvatarMenuInstaller>();
		}

		// ---- nailRoot に MenuItem ----
		ModularAvatarMenuItem rootMenuItem = nailRoot.AddComponent<ModularAvatarMenuItem>();
		SetMenuIcon(rootMenuItem, thumbnail);
		rootMenuItem.label = menuLabel;
		if (this.SplitHandFoot) {
			SetMenuSubMenu(rootMenuItem);
			rootMenuItem.MenuSource = SubmenuSource.Children;
		} else {
			// SplitHandFoot=OFF: nailRoot自体にObjectToggle（HandNail/FootNailラッパーをまとめてON/OFF）
			SetMenuToggle(rootMenuItem, 1);
			rootMenuItem.isSaved = true;
			rootMenuItem.isSynced = true;
			rootMenuItem.automaticValue = true;

			ModularAvatarObjectToggle rootToggle = nailRoot.AddComponent<ModularAvatarObjectToggle>();
			var toggleTargets = new System.Collections.Generic.List<ToggledObject>();
			// HandNailラッパー内の各ネイルオブジェクトを個別に登録
			Transform? hw = nailRoot.transform.Find(handWrapperName);
			if (hw != null) {
				foreach (Transform child in hw)
					toggleTargets.Add(new ToggledObject { Object = CreateAvatarRef(child.gameObject), Active = false });
			}
			// FootNailラッパー内の各ネイルオブジェクトを個別に登録
			if (this.UseFootNail) {
				Transform? fw = nailRoot.transform.Find(footWrapperName);
				if (fw != null) {
					foreach (Transform child in fw)
						toggleTargets.Add(new ToggledObject { Object = CreateAvatarRef(child.gameObject), Active = false });
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
						Object = CreateAvatarRef(t.gameObject),
						Active = false
					})
					.ToList();

				ModularAvatarMenuItem handMenuItem = handWrapperT.gameObject.AddComponent<ModularAvatarMenuItem>();
				SetMenuToggle(handMenuItem, 1);
				SetMenuIcon(handMenuItem, null);
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
							Object = CreateAvatarRef(t.gameObject),
							Active = false
						})
						.ToList();

					ModularAvatarMenuItem footMenuItem = footWrapperT.gameObject.AddComponent<ModularAvatarMenuItem>();
					SetMenuToggle(footMenuItem, 1);
					SetMenuIcon(footMenuItem, null);
					footMenuItem.isSaved = true;
					footMenuItem.isSynced = true;
					footMenuItem.automaticValue = true;
					footMenuItem.label = $"FootNail - {variationName}";
				}
			}
		}
	}
#endif

		/// <summary>アバターのPrefabまたはFBXのGUIDからアセットフォルダを特定する</summary>
		private string? GetAvatarAssetFolder()
		{
			// AvatarVariationDataのPrefab/FBXからフォルダを特定
			if (this.AvatarVariationData.AvatarPrefabs != null)
			{
				foreach (AvatarPrefab ap in this.AvatarVariationData.AvatarPrefabs)
				{
					if (string.IsNullOrEmpty(ap.PrefabGUID)) continue;
					string path = AssetDatabase.GUIDToAssetPath(ap.PrefabGUID);
					if (!string.IsNullOrEmpty(path))
						return Path.GetDirectoryName(path)?.Replace("\\", "/");
				}
			}
			if (this.AvatarVariationData.AvatarFbxs != null)
			{
				foreach (AvatarFbx fbx in this.AvatarVariationData.AvatarFbxs)
				{
					if (string.IsNullOrEmpty(fbx.FbxGUID)) continue;
					string path = AssetDatabase.GUIDToAssetPath(fbx.FbxGUID);
					if (!string.IsNullOrEmpty(path))
						return Path.GetDirectoryName(path)?.Replace("\\", "/");
				}
			}
			return null;
		}

		/// <summary>バリアントのパスを解決する共通メソッド（GUID検索 → ファイル名検索）</summary>
		private string? ResolveVariantPath(AvatarBlendShapeVariant variant)
		{
			// Step 1: GUID検索
			string variantPath = AssetDatabase.GUIDToAssetPath(variant.NailPrefabGUID);
			if (string.IsNullOrEmpty(variantPath) || AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) == null)
			{
				ResourceAutoExtractor.EnsurePrefabExtractedByGuid(variant.NailPrefabGUID);
				AssetDatabase.Refresh();
				variantPath = AssetDatabase.GUIDToAssetPath(variant.NailPrefabGUID);
			}
			if (string.IsNullOrEmpty(variantPath) || AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) == null)
			{
				string? diskPath = ResourceAutoExtractor.TryResolvePrefabFromDiskMeta(variant.NailPrefabGUID);
				if (!string.IsNullOrEmpty(diskPath))
				{
					AssetDatabase.ImportAsset(diskPath!);
					variantPath = diskPath!;
				}
			}
			// Step 2: [ShapeName]VariantName.prefab をファイル名で検索
			if (string.IsNullOrEmpty(variantPath) || AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) == null)
			{
				string? found = FindVariantPrefabByName(variant.Name);
				if (!string.IsNullOrEmpty(found))
				{
#if MD_NAIL_DEVELOP
					ToolConsole.Log($"Variant '{variant.Name}': ファイル名から検出 → {found}");
#endif
					variantPath = found!;
				}
			}
			if (string.IsNullOrEmpty(variantPath) || AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) == null)
				return null;
			return variantPath;
		}

		/// <summary>
		/// Prefabフォルダ内で [ShapeName]VariantName.prefab を検索する。
		/// 正しい命名規則: [ShapeName]VariantName.prefab
		/// </summary>
		private string? FindVariantPrefabByName(string variantName)
		{
			string mainPrefabPath = AssetDatabase.GetAssetPath(this.NailPrefab);
			string mainFileName = Path.GetFileNameWithoutExtension(mainPrefabPath);
			var shapeMatch = Regex.Match(mainFileName, @"\[(?<shape>.+)\].+");
			string shapeName = shapeMatch.Success ? shapeMatch.Groups["shape"].Value : "Natural";

			// 期待されるファイル名: [ShapeName]VariantName.prefab
			string expectedFileName = $"[{shapeName}]{variantName}.prefab";

			string[] searchRoots = {
				"Assets/[An-Labo.Virtual]/An-Labo Nail Tool/Resource/Nail/Prefab",
				"Packages/world.anlabo.mdnailtool/Nail/Prefab"
			};

			// アバターフォルダも検索対象に追加
			string? avatarFolder = GetAvatarAssetFolder();
			List<string> allRoots = new(searchRoots);
			if (!string.IsNullOrEmpty(avatarFolder)) allRoots.Add(avatarFolder!);

			foreach (string root in allRoots)
			{
				string fullRoot = Path.GetFullPath(root);
				if (!Directory.Exists(fullRoot)) continue;

				try
				{
					foreach (string file in Directory.EnumerateFiles(fullRoot, expectedFileName, SearchOption.AllDirectories))
					{
						string assetPath = file.Replace("\\", "/");
						int idx = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
						if (idx >= 0) assetPath = assetPath.Substring(idx);
						return assetPath;
					}
				}
				catch { /* skip */ }
			}

			return null;
		}

		/// <summary>
		/// デバッグ用の診断情報を生成する。
		/// バージョン、着用設定、アバター情報を含む。
		/// </summary>
		private string BuildDiagnosticInfo(bool includeFolder = false)
		{
			bool isJa = LanguageManager.CurrentLanguageData.language == "ja";
			var sb = new System.Text.StringBuilder();
			sb.AppendLine();
			sb.AppendLine(isJa ? "--- 診断情報 ---" : "--- Diagnostic Info ---");

			try { sb.AppendLine($"NailTool Version: {MDNailToolDefines.Version}"); }
			catch { sb.AppendLine(isJa ? "NailTool Version: (取得失敗)" : "NailTool Version: (unavailable)"); }

			sb.AppendLine($"ModularAvatar: {GetModularAvatarVersion()}");

			sb.AppendLine($"Avatar: {this.Avatar?.gameObject?.name ?? "(null)"}");
			sb.AppendLine($"Avatar Root Scale: {this.Avatar?.transform?.localScale.ToString() ?? "(null)"}");
			sb.AppendLine($"AvatarName: {this.AvatarName ?? (isJa ? "(未設定)" : "(not set)")}");
			sb.AppendLine($"Variation: {this.AvatarVariationData?.VariationName ?? "(null)"}");
			sb.AppendLine($"NailShape: {this.NailShapeName}");
			sb.AppendLine($"NailPrefab: {this.NailPrefab?.name ?? "(null)"}");
			sb.AppendLine($"ForModularAvatar: {this.ForModularAvatar}");
			sb.AppendLine($"BakeBlendShapes: {this.BakeBlendShapes}");
			sb.AppendLine($"SyncBlendShapesWithMA: {this.SyncBlendShapesWithMA}");
			sb.AppendLine($"ArmatureScaleCompensation: {this.ArmatureScaleCompensation}");
			sb.AppendLine($"UseFootNail: {this.UseFootNail}");
			sb.AppendLine($"GenerateMaterial: {this.GenerateMaterial}");

			if (includeFolder)
			{
				string listing = ListPrefabFolderContents();
				if (!string.IsNullOrEmpty(listing))
				{
					sb.AppendLine(isJa ? "--- Resourceフォルダ内容 ---" : "--- Resource Folder Contents ---");
					sb.Append(listing);
				}
			}

			return sb.ToString();
		}

		private static string GetModularAvatarVersion()
		{
			try
			{
				string packageJsonPath = "Packages/nadena.dev.modular-avatar/package.json";
				TextAsset? packageJson = AssetDatabase.LoadAssetAtPath<TextAsset>(packageJsonPath);
				if (packageJson != null)
				{
					var json = Newtonsoft.Json.Linq.JObject.Parse(packageJson.text);
					return json["version"]?.ToString() ?? "unknown";
				}
			}
			catch { /* ignore */ }

			return "not installed";
		}

		/// <summary>
		/// Nail/Prefab フォルダ内の .prefab ファイル一覧を返す（デバッグ用）。
		/// </summary>
		private static string ListPrefabFolderContents()
		{
			string[] searchRoots = {
				"Assets/[An-Labo.Virtual]/An-Labo Nail Tool/Resource/Nail/Prefab",
				"Packages/world.anlabo.mdnailtool/Resource/Nail/Prefab"
			};

			var sb = new System.Text.StringBuilder();
			foreach (string root in searchRoots)
			{
				string fullRoot = Path.GetFullPath(root);
				if (!Directory.Exists(fullRoot)) continue;

				sb.AppendLine($"[{root}]");
				try
				{
					foreach (string dir in Directory.GetDirectories(fullRoot))
					{
						string dirName = Path.GetFileName(dir);
						string[] prefabs = Directory.GetFiles(dir, "*.prefab");
						if (prefabs.Length == 0)
						{
							sb.AppendLine($"  {dirName}/ (empty)");
							continue;
						}
						sb.AppendLine($"  {dirName}/");
						foreach (string prefab in prefabs)
							sb.AppendLine($"    {Path.GetFileName(prefab)}");
					}

					string[] rootPrefabs = Directory.GetFiles(fullRoot, "*.prefab");
					foreach (string prefab in rootPrefabs)
						sb.AppendLine($"  {Path.GetFileName(prefab)}");
				}
				catch (Exception e)
				{
					sb.AppendLine($"  (読み取りエラー: {e.Message})");
				}
			}
			return sb.ToString();
		}
	}
}
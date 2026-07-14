using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
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
	public class NailSetupUserException : NailToolUserException {
		public NailSetupUserException(string message,
			[CallerFilePath] string file = "",
			[CallerMemberName] string method = "",
			[CallerLineNumber] int line = 0)
			: base("NailSetup", message, null, file, method, line) { }
	}

	public partial class NailSetupProcessor {
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
		private NailPrefabNodeData[]? SelectedBlendShapeVariantNailNodes { get; set; }
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
			ValidateAvatarRig();

			INailProcessor.ClearCreatedMaterialCash();
			Undo.IncrementCurrentGroup();

			ApplySelectedVariantPrefab();
			ResolveShapePrefabForCurrentShape();
			GameObject nailPrefabObject = InstantiateAndLabelNailPrefab();
			Undo.RegisterCreatedObjectUndo(nailPrefabObject, "Nail Setup");

			string prefix = this.getPrefabPrefix();

			// NailPrefabBuilder.BuildFromNodes 出力 (in-memory orphan) は Scene root に残り Default-Material のままマゼンタ描画されるため、prefix 取得後に destroy する.
			if (this.NailPrefab != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(this.NailPrefab))) {
				Object.DestroyImmediate(this.NailPrefab);
			}

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

			for (int i = 0; i < 10 && i < handsNailObjects.Length; i++) {
				if (i < this.NailDesignAndVariationNames.Length && this.NailDesignAndVariationNames[i].Item1 == null && handsNailObjects[i] != null) {
					UnityEngine.Object.DestroyImmediate(handsNailObjects[i]!.gameObject);
					handsNailObjects[i] = null;
				}
			}
			for (int i = 0; i < 5 && i < leftFootNailObjects.Length; i++) {
				int designIdx = 10 + i;
				if (designIdx < this.NailDesignAndVariationNames.Length && this.NailDesignAndVariationNames[designIdx].Item1 == null && leftFootNailObjects[i] != null) {
					UnityEngine.Object.DestroyImmediate(leftFootNailObjects[i]!.gameObject);
					leftFootNailObjects[i] = null;
				}
			}
			for (int i = 0; i < 5 && i < rightFootNailObjects.Length; i++) {
				int designIdx = 15 + i;
				if (designIdx < this.NailDesignAndVariationNames.Length && this.NailDesignAndVariationNames[designIdx].Item1 == null && rightFootNailObjects[i] != null) {
					UnityEngine.Object.DestroyImmediate(rightFootNailObjects[i]!.gameObject);
					rightFootNailObjects[i] = null;
				}
			}

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
				ToolConsole.Warn("NailSetup", $"{LanguageManager.S("warn.mip_streaming_failed") ?? "Failed to enable Mip Streaming"}: {e.Message}{BuildDiagnosticInfo()}");
			}

			// ---- BlendShapeのベイクとMA同期設定 ----
			List<(SkinnedMeshRenderer sourceSmr, string sourcePath)> resolvedSourceSmrs =
				ResolveBlendShapeSyncSources();
			BakeBlendShapesIfNeeded(resolvedSourceSmrs, handsNailObjects, leftFootNailObjects, rightFootNailObjects);

			// ---- ネイルSMRのlocalBoundsを広めに固定(フラスタムカリング・最適化対策)----
			// MA MeshSettings 未適用時のフォールバック。Bounds ベースの可視性判定にも対応する。
			ApplyNailBoundsGuard(handsNailObjects);
			if (this.UseFootNail) {
				ApplyNailBoundsGuard(leftFootNailObjects);
				ApplyNailBoundsGuard(rightFootNailObjects);
			}

			// scale退避→配置→復元。歪み防止 (Save/Compute も try 内に置き、途中例外でも finally で必ず Restore する)
			Dictionary<Transform, Vector3> savedBoneScales = new();
			Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>? corrections = null;
			try
			{
				corrections =
					ComputeArmatureScaleCorrections(handsNailObjects, leftFootNailObjects, rightFootNailObjects, targetBoneDictionary, ref savedBoneScales);

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


			CleanupOrphanedNailPrefabsInScene();

			SchedulePostSetupRefresh(nailPrefabObject);
		}

		// Scene root に取り残された NailPrefabBuilder.BuildFromNodes 出力 (parent=null, SMR が Default-Material のみ) を一掃する.
		private static void CleanupOrphanedNailPrefabsInScene() {
			UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
			if (!scene.IsValid()) return;
			Regex shapePrefixPattern = new(@"^\[(?:[^\]]+)\]");
			foreach (GameObject go in scene.GetRootGameObjects()) {
				if (go == null) continue;
				if (go.transform.parent != null) continue;
				if (!shapePrefixPattern.IsMatch(go.name)) continue;
				SkinnedMeshRenderer[] smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
				if (smrs.Length == 0) continue;
				bool junk = true;
				foreach (SkinnedMeshRenderer smr in smrs) {
					foreach (Material m in smr.sharedMaterials) {
						if (m != null && m.name != "Default-Material") { junk = false; break; }
					}
					if (!junk) break;
				}
				if (junk) Object.DestroyImmediate(go);
			}
		}

		// アバターの Animator / Humanoid Rig をチェックし、欠落時はユーザー向け例外を投げる.
		private void ValidateAvatarRig()
		{
			Animator avatarAnimator = this.Avatar.GetComponent<Animator>();
			if (avatarAnimator == null) {
				throw new NailSetupUserException(LanguageManager.S("error.execute.no_animator") ?? "error.execute.no_animator");
			}
			if (avatarAnimator.avatar == null) {
				throw new NailSetupUserException(LanguageManager.S("error.execute.no_avatar_rig") ?? "error.execute.no_avatar_rig");
			}
		}

		// SelectedBlendShapeVariantName が指定されていればベース NailPrefab をバリアント差し替えする.
		private void ApplySelectedVariantPrefab()
		{
			this.SelectedBlendShapeVariantNailNodes = null;
			ToolConsole.Log($"  SelectedBlendShapeVariantName={this.SelectedBlendShapeVariantName ?? "(null)"}");
			if (string.IsNullOrEmpty(this.SelectedBlendShapeVariantName))
			{
				ToolConsole.Log("  → no variant selected, using base prefab");
				return;
			}

			AvatarBlendShapeVariant[]? activeVariants = this.AvatarVariationData.BlendShapeVariants ?? this.AvatarEntity?.BlendShapeVariants;
			ToolConsole.Log($"  activeVariants null? {activeVariants == null}, count={activeVariants?.Length ?? 0}");
			if (activeVariants == null) return;

			ToolConsole.Log($"  activeVariants names: [{string.Join(", ", activeVariants.Select(v => v.Name))}]");
			AvatarBlendShapeVariant? variant = activeVariants.FirstOrDefault(v => v.Name == this.SelectedBlendShapeVariantName);
			ToolConsole.Log($"  variant match? {variant != null}, GUID={variant?.NailPrefabGUID ?? "(null)"}, NailNodes={variant?.NailNodes?.Length ?? 0}");
			if (variant == null) return;

			if (variant.NailNodes != null && variant.NailNodes.Length > 0)
			{
				this.SelectedBlendShapeVariantNailNodes = variant.NailNodes;
				this.NailPrefab = NailPrefabBuilder.BuildFromNodes(variant.NailNodes, variant.Name);
				ToolConsole.Log($"  → NailPrefab replaced from NailNodes: {variant.Name}");
				return;
			}

			if (string.IsNullOrEmpty(variant.NailPrefabGUID)) return;

			string? variantPath = ResolveVariantPath(variant);
			ToolConsole.Log($"  variantPath={variantPath ?? "(null)"}");
			if (string.IsNullOrEmpty(variantPath)) return;

			GameObject? variantPrefab = NailSetupUtil.LoadPrefabAtPath(variantPath);
			ToolConsole.Log($"  variantPrefab={variantPrefab?.name ?? "(null)"}");
			if (variantPrefab == null) return;

			this.NailPrefab = variantPrefab;
			ToolConsole.Log($"  → NailPrefab replaced: {variantPrefab.name}");
		}

		// 現在の NailShapeName に対応する [shape]Name.prefab を探して NailPrefab を差し替える.
		private void ResolveShapePrefabForCurrentShape()
		{
			string prefabPath = AssetDatabase.GetAssetPath(this.NailPrefab);

			// NailNodes 経路: 不在 shape の fallback は ResolveShapePrefab と同様「collection 順で target まで walk して最新存在」.
			if (string.IsNullOrEmpty(prefabPath)) {
				NailPrefabNodeData[]? allNodes = this.SelectedBlendShapeVariantNailNodes ?? this.AvatarVariationData?.NailNodes;
				if (allNodes != null && allNodes.Length > 0) {
					NailPrefabNodeData[]? currentShapeNodes = null;
					using DBNailShape dbNailShapeFb = new();
					foreach (NailShape ns in dbNailShapeFb.collection) {
						string p = $"[{ns.ShapeName}]";
						NailPrefabNodeData[] found = System.Array.FindAll(allNodes, n => n.Name != null && n.Name.StartsWith(p));
						if (found.Length > 0) currentShapeNodes = found;
						if (ns.ShapeName == this.NailShapeName) break;
					}
					if (currentShapeNodes != null) {
						Object.DestroyImmediate(this.NailPrefab);
						this.NailPrefab = NailPrefabBuilder.BuildFromNodes(currentShapeNodes, this.SelectedBlendShapeVariantName ?? this.AvatarVariationData!.VariationName);
					}
				}
				return;
			}

			Regex nailPrefabNamePattern = new(@"(?<prefix>\[.+\])(?<prefabName>.+)");
			Match match = nailPrefabNamePattern.Match(this.NailPrefab.name);
			if (!match.Success) return;

			string prefabName = match.Groups["prefabName"].Value;
			// Path.GetDirectoryName は Windows で `\` 区切りを返す. AssetDatabase は `/` 前提のため正規化する.
			string prefabDirPath = (Path.GetDirectoryName(prefabPath) ?? "").Replace('\\', '/');
			GameObject current = this.NailPrefab;
			using DBNailShape dbNailShape = new();
			foreach (NailShape nailShape in dbNailShape.collection) {
				string newPrefabPath = $"{prefabDirPath}/[{nailShape.ShapeName}]{prefabName}.prefab";
				if (File.Exists(newPrefabPath)) {
					GameObject? newPrefab = NailSetupUtil.LoadPrefabAtPath(newPrefabPath);
					if (newPrefab != null) {
						current = newPrefab;
					}
				}
				if (nailShape.ShapeName == this.NailShapeName) break;
			}
			this.NailPrefab = current;
		}

		// NailPrefab をアバター配下に Instantiate し [An-Labo]デザイン名_カラー名 でラベル付けする.
		private GameObject InstantiateAndLabelNailPrefab()
		{
			if (this.NailPrefab == null) {
				ToolConsole.Log($"[NailDiag] NailPrefab=null. Avatar='{this.Avatar?.gameObject.name}' AvatarVariation='{this.AvatarVariationData?.VariationName ?? "(null)"}' SelectedBSV='{this.SelectedBlendShapeVariantName ?? "(null)"}' BaseGUID='{this.AvatarVariationData?.NailPrefabGUID ?? "(null)"}' Shape='{this.NailShapeName}'");
				throw new NailSetupUserException(LanguageManager.S("error.execute.nail_prefab_load_failed") ?? "error.execute.nail_prefab_load_failed");
			}
			GameObject nailPrefabObject = Object.Instantiate(this.NailPrefab, this.Avatar.transform);
			var firstEntry = this.NailDesignAndVariationNames.FirstOrDefault(t => t.Item1 != null);
			string designName = firstEntry.Item1 != null
				? firstEntry.Item1.DesignName
				: (this.OverrideMaterial?.name ?? "Unknown");
			string colorName = firstEntry.Item1 != null ? firstEntry.Item3 : "";
			string nailLabel = string.IsNullOrEmpty(colorName) ? designName : $"{designName}_{colorName}";
			nailPrefabObject.name = $"[An-Labo]{nailLabel}";
			return nailPrefabObject;
		}

		// ArmatureScaleCompensation=true の時に Bone Scale を退避し、ネイル位置補正テーブルを生成する.
		// 退避 scale は呼び出し側 finally で必ず Restore する必要があるため ref で外に渡す.
		private Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>?
			ComputeArmatureScaleCorrections(
				Transform?[] handsNailObjects, Transform?[] leftFootNailObjects, Transform?[] rightFootNailObjects,
				Dictionary<string, Transform?> targetBoneDictionary,
				ref Dictionary<Transform, Vector3> savedBoneScales)
		{
			if (!this.ArmatureScaleCompensation) return null;

			SaveAndNeutralizeBoneScales(this.Avatar, savedBoneScales);

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
			return ComputeScaleCompensatedTransforms(
				this.Avatar, targetBoneDictionary,
				allNails.ToArray(), allBoneIndices.ToArray());
		}

		// 装着完了後の Editor 表示リフレッシュ (bake 直後の SMR 描画キャッシュ問題対策).
		// 生成された ネイル SMR のみを対象 + 1 frame 遅延で MA pipeline 完了後に走らせる.
		private static void SchedulePostSetupRefresh(GameObject nailRoot)
		{
			GameObject capturedNailRoot = nailRoot;
			EditorApplication.delayCall += () => {
				if (capturedNailRoot == null) return;
				foreach (SkinnedMeshRenderer smr in capturedNailRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
				{
					if (smr == null) continue;
					bool prev = smr.enabled;
					smr.enabled = false;
					smr.enabled = prev;
					EditorUtility.SetDirty(smr);
				}
				SceneView.RepaintAll();
			};
		}


	}
}

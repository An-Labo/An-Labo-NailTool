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
		public bool[]? EnabledNailSlots { get; set; }
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
		public string? SelectedBlendShapeVariantName { get; set; }
		private NailPrefabNodeData[]? SelectedBlendShapeVariantNailNodes { get; set; }
		public Entity.Avatar? AvatarEntity { get; set; }
		public bool EnableAdditionalMaterials { get; set; } = true;
		public IEnumerable<Material>?[]? PerFingerAdditionalMaterials { get; set; }
		public IEnumerable<Transform>?[]? PerFingerAdditionalObjects { get; set; }
		// Execute builds decorations only after preflight and inside the setup ownership scope.
		public Func<IEnumerable<Transform>?[]?>? AdditionalObjectsFactory { get; set; }

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

		private bool ShouldRemoveNailSlot(int index) {
			if (index < 0 || index >= this.NailDesignAndVariationNames.Length) return true;
			if (this.NailDesignAndVariationNames[index].Item1 != null) return false;

			// 外部マテリアル直接指定時は、An-Labo ネイルが未選択でもONの指を維持する。
			return this.OverrideMaterial == null
			       || this.EnabledNailSlots == null
			       || index >= this.EnabledNailSlots.Length
			       || !this.EnabledNailSlots[index];
		}


		public void Process() {
			using var temporaryPrefabs = NailPrefabBuilder.BeginTemporaryScope(this.NailPrefab);
			ValidateAvatarRig();
			ValidateHandBoneOverrides();
			// 装着対象ボーンの取得
			Dictionary<string, Transform?> targetBoneDictionary = GetTargetBoneDictionary(this.Avatar, this.AvatarVariationData.BoneMappingOverride);

			// 指ボーン存在チェック
			bool hasAnyFingerBone = MDNailToolDefines.TARGET_HANDS_BONE_NAME_LIST
				.Any(name => targetBoneDictionary.ContainsKey(name) && targetBoneDictionary[name] != null);
			if (!hasAnyFingerBone) {
				throw new NailSetupUserException(LanguageManager.S("error.execute.no_finger_bones") ?? "error.execute.no_finger_bones");
			}


			using var transaction = new NailSetupTransaction(this.Avatar.gameObject);
			ProcessCore(targetBoneDictionary);
			transaction.Complete();
		}

		private void ProcessCore(Dictionary<string, Transform?> targetBoneDictionary) {
			INailProcessor.ClearCreatedMaterialCash();
			if (this.AdditionalObjectsFactory != null)
				this.PerFingerAdditionalObjects = this.AdditionalObjectsFactory();

			ApplySelectedVariantPrefab();
			ResolveShapePrefabForCurrentShape();
			GameObject nailPrefabObject = InstantiateAndLabelNailPrefab();

			string prefix = this.getPrefabPrefix();

			// Release only a source explicitly created as temporary by this tool.
			NailPrefabBuilder.DestroyTemporaryPrefab(this.NailPrefab);

			if (!string.IsNullOrEmpty(prefix)) {
				foreach (Transform child in nailPrefabObject.transform) {
					child.name = child.name.Replace(prefix, "");
				}
			}

			// プレハブ内のネイルオブジェクトを取得
			Transform?[] handsNailObjects = GetHandsNailObjectList(nailPrefabObject);
			Transform?[] leftFootNailObjects = GetLeftFootNailObjectList(nailPrefabObject);
			Transform?[] rightFootNailObjects = GetRightFootNailObjectList(nailPrefabObject);

			for (int i = 0; i < 10 && i < handsNailObjects.Length; i++) {
				if (i < this.NailDesignAndVariationNames.Length && this.ShouldRemoveNailSlot(i) && handsNailObjects[i] != null) {
					UnityEngine.Object.DestroyImmediate(handsNailObjects[i]!.gameObject);
					handsNailObjects[i] = null;
				}
			}
			for (int i = 0; i < 5 && i < leftFootNailObjects.Length; i++) {
				int designIdx = 10 + i;
				if (designIdx < this.NailDesignAndVariationNames.Length && this.ShouldRemoveNailSlot(designIdx) && leftFootNailObjects[i] != null) {
					UnityEngine.Object.DestroyImmediate(leftFootNailObjects[i]!.gameObject);
					leftFootNailObjects[i] = null;
				}
			}
			for (int i = 0; i < 5 && i < rightFootNailObjects.Length; i++) {
				int designIdx = 15 + i;
				if (designIdx < this.NailDesignAndVariationNames.Length && this.ShouldRemoveNailSlot(designIdx) && rightFootNailObjects[i] != null) {
					UnityEngine.Object.DestroyImmediate(rightFootNailObjects[i]!.gameObject);
					rightFootNailObjects[i] = null;
				}
			}

			ValidateSelectedNailMeshes(handsNailObjects, leftFootNailObjects, rightFootNailObjects, this.UseFootNail);

			if (this.RemoveCurrentNail) {
				RemoveNail(this.Avatar, targetBoneDictionary);
			}

			// メッシュの適用
			if (this.OverrideMesh is { Length: > 0 }) {
				try {
					NailSetupUtil.ReplaceHandsNailMesh(handsNailObjects, this.OverrideMesh);
				} catch (Exception) {
					throw;
				}
			}

			// 足のメッシュの適用
			try {
				NailSetupUtil.ReplaceFootNailMesh(leftFootNailObjects, rightFootNailObjects, this.NailShapeName);
			} catch (Exception) {
				throw;
			}

			// マテリアルの適用
			try {
				NailSetupUtil.ReplaceNailMaterial(handsNailObjects, leftFootNailObjects, rightFootNailObjects, this.NailDesignAndVariationNames, this.NailShapeName, this.GenerateMaterial, false, this.OverrideMaterial,
					this.EnableAdditionalMaterials, this.PerFingerAdditionalMaterials);
			} catch (Exception) {
				throw;
			}


			try {
				NailSetupUtil.AttachAdditionalObjects(handsNailObjects, this.NailDesignAndVariationNames, this.NailShapeName, false, this.PerFingerAdditionalObjects);
			} catch (Exception) {
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
						foreach (Transform additionalObject in fingerObjects) {
							NailSetupTransaction.TrackCreated(additionalObject.gameObject);
							additionalObject.SetParent(footNailObjects[fi], false);
						}
					}
				} catch (Exception) {
					throw;
				}
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

			// Armature の現在の実ボーン変換を使って補正する。
			// ここでボーン scale を 1 に戻すと、極端に縮小された腕などで指先からのオフセットだけが縮小されず残る。
			Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>? corrections = null;
			corrections =
				ComputeArmatureScaleCorrections(handsNailObjects, leftFootNailObjects, rightFootNailObjects, targetBoneDictionary);

			if (this.ForModularAvatar) {
				SetupForModularAvatar(nailPrefabObject, targetBoneDictionary, handsNailObjects,
					leftFootNailObjects, rightFootNailObjects, resolvedSourceSmrs, corrections);
			} else {
				SetupDirect(nailPrefabObject, targetBoneDictionary, handsNailObjects,
					leftFootNailObjects, rightFootNailObjects, corrections);
			}


			// Mip Streaming有効化
			try {
				// Combine may have replaced the original nail objects by this point.
				var allRenderers = NailSetupTransaction.GetCreatedRenderers();
				NailSetupUtil.EnableMipStreamingForRenderers(allRenderers);
			} catch (Exception e) {
				ToolConsole.Warn("NailSetup", $"{LanguageManager.S("warn.mip_streaming_failed") ?? "Failed to enable Mip Streaming"}: {e.Message}{BuildDiagnosticInfo()}");
			}

			SchedulePostSetupRefresh(nailPrefabObject);
		}

		private static void ValidateSelectedNailMeshes(
			Transform?[] handsNailObjects,
			Transform?[] leftFootNailObjects,
			Transform?[] rightFootNailObjects,
			bool useFootNail)
		{
			var missing = new List<string>();

			void Check(IEnumerable<Transform?> nailObjects)
			{
				foreach (Transform? nailObject in nailObjects)
				{
					if (nailObject == null) continue;
					SkinnedMeshRenderer? smr = nailObject.GetComponentInChildren<SkinnedMeshRenderer>(true);
					if (smr == null || smr.sharedMesh == null) missing.Add(nailObject.name);
				}
			}

			Check(handsNailObjects);
			if (useFootNail)
			{
				Check(leftFootNailObjects);
				Check(rightFootNailObjects);
			}

			if (missing.Count == 0) return;

			string template = LanguageManager.S("error.execute.nail_mesh_missing")
				?? "Nail mesh resources are missing: {0}. Please reinstall the [An-Labo.Virtual] resources.";
			ToolConsole.Error("NailSetup", string.Format(template, string.Join(", ", missing.Distinct())));
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
				NailPrefabNodeData[] baseNodes = (this.AvatarVariationData.NailNodes ?? Array.Empty<NailPrefabNodeData>())
					.Concat(this.AvatarVariationData.FootNailNodes ?? Array.Empty<NailPrefabNodeData>()).ToArray();
				NailPrefabNodeData[] scaledVariantNodes = ComposeVariantNodes(
					baseNodes,
					variant.NailNodes);
				this.SelectedBlendShapeVariantNailNodes = scaledVariantNodes;
				this.NailPrefab = NailPrefabBuilder.BuildTemporaryFromNodes(scaledVariantNodes, variant.Name, this.NailShapeName);
				ToolConsole.Log($"  → NailPrefab composed from base + variant NailNodes: {variant.Name}");
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
					NailPrefabNodeData[]? currentShapeNodes = ComposeShapeNodes(allNodes, this.NailShapeName);
					if (currentShapeNodes != null) {
						NailPrefabBuilder.DestroyTemporaryPrefab(this.NailPrefab);
						this.NailPrefab = NailPrefabBuilder.BuildTemporaryFromNodes(currentShapeNodes, this.SelectedBlendShapeVariantName ?? this.AvatarVariationData!.VariationName, this.NailShapeName);
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
			NailSetupTransaction.TrackCreated(nailPrefabObject);
			var firstEntry = this.NailDesignAndVariationNames.FirstOrDefault(t => t.Item1 != null);
			string designName = firstEntry.Item1 != null
				? firstEntry.Item1.DesignName
				: (this.OverrideMaterial?.name ?? "Unknown");
			string colorName = firstEntry.Item1 != null ? firstEntry.Item3 : "";
			string nailLabel = string.IsNullOrEmpty(colorName) ? designName : $"{designName}_{colorName}";
			nailPrefabObject.name = $"[An-Labo]{nailLabel}";
			return nailPrefabObject;
		}

		// ArmatureScaleCompensation=true の時に、現在の Armature 形状に合わせたネイル位置補正テーブルを生成する.
		private Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>?
			ComputeArmatureScaleCorrections(
				Transform?[] handsNailObjects, Transform?[] leftFootNailObjects, Transform?[] rightFootNailObjects,
				Dictionary<string, Transform?> targetBoneDictionary)
		{
			if (!this.ArmatureScaleCompensation) return null;

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
    // One synchronous setup owns its undo group and explicit creation references.
    // Scene inventories are only a protection boundary, never a deletion candidate list.
    internal sealed class NailSetupTransaction : IDisposable
    {
        [ThreadStatic] private static NailSetupTransaction? current;
        private readonly NailSetupTransaction? previous;
        private readonly int group;
        private readonly HashSet<int> existingObjects;
        private readonly HashSet<GameObject> created = new();
        private readonly Dictionary<string, string> createdAssets = new();
        private readonly HashSet<Object> changedAssets = new();
        private bool completed;

        internal NailSetupTransaction(GameObject avatar)
        {
            existingObjects = new HashSet<int>(Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(o => o.scene.IsValid()).Select(o => o.GetInstanceID()));
            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Nail Setup");
            Undo.RegisterFullObjectHierarchyUndo(avatar, "Nail Setup");
            previous = current;
            current = this;
        }

        internal static bool TrackCreated(GameObject root)
        {
            if (current == null || root == null || EditorUtility.IsPersistent(root)) return false;
            if (current.existingObjects.Contains(root.GetInstanceID())) return false;
            if (current.created.Contains(root)) return true;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (!current.existingObjects.Contains(child.gameObject.GetInstanceID()))
                    current.created.Add(child.gameObject);
            Undo.RegisterCreatedObjectUndo(root, "Nail Setup");
            return true;
        }

        internal static void RecordAssetChange(Object asset)
        {
            if (current == null || asset == null) return;
            string path = AssetDatabase.GetAssetPath(asset);
            if (current.createdAssets.ContainsKey(path)) return;
            if (current.changedAssets.Add(asset))
                Undo.RegisterCompleteObjectUndo(asset, "Nail Setup Asset");
        }

        internal static IEnumerable<Renderer> GetCreatedRenderers()
        {
            return current == null ? Enumerable.Empty<Renderer>()
                : current.created.Where(o => o != null).SelectMany(o => o.GetComponents<Renderer>());
        }

        internal static void CreateGeneratedAsset(Object asset, string path)
        {
            // Never replace an earlier generated asset on a same-second/path collision.
            string uniquePath = path;
            try
            {
                uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);
                AssetDatabase.CreateAsset(asset, uniquePath);
                string guid = AssetDatabase.AssetPathToGUID(uniquePath);
                if (current != null && AssetDatabase.GetAssetPath(asset) == uniquePath && !string.IsNullOrEmpty(guid))
                    current.createdAssets[uniquePath] = guid;
                if (string.IsNullOrEmpty(uniquePath) || AssetDatabase.GetAssetPath(asset) != uniquePath
                    || string.IsNullOrEmpty(guid) || !File.Exists(uniquePath))
                    throw new IOException($"Generated asset was not saved: {uniquePath}");
            }
            catch (Exception ex)
            {
                throw new NailToolUserException("NailSetup", $"Could not save generated asset: {uniquePath}", ex);
            }
        }

        internal void Complete()
        {
            Undo.CollapseUndoOperations(group);
            completed = true;
        }

        public void Dispose()
        {
            current = previous;
            try
            {
                if (completed) return;
                // Revert registered deletions and property/component changes first.
                Undo.RevertAllDownToGroup(group);
                // A generated child may have left its initial root before an exception.
                foreach (GameObject obj in created.Reverse())
                {
                    if (obj == null || EditorUtility.IsPersistent(obj)) continue;
                    // Refuse to destroy a generated root if it now owns an original object.
                    if (obj.GetComponentsInChildren<Transform>(true)
                        .Any(t => existingObjects.Contains(t.gameObject.GetInstanceID()))) continue;
                    Object.DestroyImmediate(obj);
                }
                foreach (var entry in createdAssets)
                    if (!string.IsNullOrEmpty(entry.Value)
                        && AssetDatabase.AssetPathToGUID(entry.Key) == entry.Value)
                        AssetDatabase.DeleteAsset(entry.Key);
                foreach (Object asset in changedAssets)
                    if (asset != null && EditorUtility.IsPersistent(asset)) EditorUtility.SetDirty(asset);
                if (changedAssets.Count > 0) AssetDatabase.SaveAssets();
                INailProcessor.ClearCreatedMaterialCash();
            }
            finally
            {
                // A later operation must never join this setup's undo group.
                Undo.IncrementCurrentGroup();
            }
        }
    }

}

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Core;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Editor.VisualElements;
using static world.anlabo.mdnailtool.Editor.Language.LanguageManager;
using Avatar = world.anlabo.mdnailtool.Editor.Entity.Avatar;
using Object = UnityEngine.Object;
using Newtonsoft.Json;
using world.anlabo.mdnailtool.Editor;
using world.anlabo.mdnailtool.Editor.Window.Domain;
using world.anlabo.mdnailtool.Editor.Window.Controllers;

namespace world.anlabo.mdnailtool.Editor.Window
{
	public partial class MDNailToolWindow
	{
		private void OnSelectNail(string designName)
		{
			ResourceAutoExtractor.EnsureDesignExtracted(designName);
			using DBNailDesign dbNailDesign = new();
			NailDesign? design = dbNailDesign.FindNailDesignByDesignName(designName);
			if (design?.DesignName == null) return;
			INailProcessor nailProcessor = INailProcessor.CreateNailDesign(design.DesignName);

			List<string> materialPopupElements = design.MaterialVariation != null ?
				design.MaterialVariation.Where(pair => nailProcessor.IsInstalledMaterialVariation(pair.Value.MaterialName)).Select(pair => pair.Value.MaterialName).ToList() : new List<string> { "" };
			string materialValue = materialPopupElements.FirstOrDefault() ?? "";

			List<string> colorPopupElements = design.ColorVariation
				.Where(pair => nailProcessor.IsInstalledColorVariation(materialValue, pair.Value.ColorName)).Select(pair => pair.Value.ColorName).ToList();
			string colorValue = colorPopupElements.FirstOrDefault() ?? "";

			foreach (NailDesignDropDowns nailDesignDropDowns in this._nailDesignDropDowns!)
			{
				nailDesignDropDowns.SetValue(designName, materialValue, materialPopupElements, colorValue, colorPopupElements);
			}

			this._nailMaterialDropDown!.choices = materialPopupElements;
			this._nailMaterialDropDown!.SetValueWithoutNotify(materialValue);
			this._nailColorDropDown!.choices = colorPopupElements;
			this._nailColorDropDown!.SetValueWithoutNotify(colorValue);

			// 追加マテリアル・追加オブジェクトのデフォルトをプレビュー前に設定
			this.UpdateAdditionalMaterialSourceDefault();
			this.UpdateAdditionalObjectSourceDefault();

			this.UpdateNailShapeFilter(nailProcessor);
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();

			// バリアントドロップダウンを更新（マテリアルバリエーション欄と入れ替え表示）
			string? parentName = design.ParentVariant;
			string parentDesignName = !string.IsNullOrEmpty(parentName) ? parentName! : designName;
			IReadOnlyList<NailDesign> variantChildren = dbNailDesign.FindChildVariants(parentDesignName);
			if (variantChildren.Count > 0 && this._nailVariantDropDown != null)
			{
				string langKey = CurrentLanguageData.language;
				this._variantDisplayNames = new Dictionary<string, string>();
				var variantChoices = new List<string>();

				// 親
				NailDesign? parentDesign = dbNailDesign.FindNailDesignByDesignName(parentDesignName);
				if (parentDesign != null)
				{
					string pDisplay = parentDesign.DisplayNames?.GetValueOrDefault(langKey, parentDesign.DesignName) ?? parentDesign.DesignName;
					this._variantDisplayNames[parentDesignName] = pDisplay;
					variantChoices.Add(parentDesignName);
				}

				// 子
				foreach (NailDesign child in variantChildren)
				{
					string cDisplay = child.DisplayNames?.GetValueOrDefault(langKey, child.DesignName) ?? child.DesignName;
					this._variantDisplayNames[child.DesignName] = cDisplay;
					variantChoices.Add(child.DesignName);
				}

				var displayMap = this._variantDisplayNames;
				this._nailVariantDropDown.choices = variantChoices;
				this._nailVariantDropDown.SetValueWithoutNotify(designName);
				this._nailVariantDropDown.formatListItemCallback = name =>
					name != null && displayMap.TryGetValue(name, out string? dn) ? dn : name ?? "";
				this._nailVariantDropDown.formatSelectedValueCallback = name =>
					name != null && displayMap.TryGetValue(name, out string? dn) ? dn : name ?? "";

				// マテリアルドロップダウンを隠し、バリアントドロップダウンを表示
				this._nailMaterialDropDown!.style.display = DisplayStyle.None;
				this._nailVariantDropDown.style.display = DisplayStyle.Flex;
			}
			else
			{
				// バリアントなし → マテリアルドロップダウンを表示、バリアントを隠す
				this._nailMaterialDropDown!.style.display = DisplayStyle.Flex;
				if (this._nailVariantDropDown != null)
				{
					this._nailVariantDropDown.style.display = DisplayStyle.None;
				}
			}
		}

		private void OnChangeNailVariantDropDown(ChangeEvent<string> evt)
		{
			if (string.IsNullOrEmpty(evt.newValue)) return;
			this.OnSelectNail(evt.newValue);
		}

		private void OnChangeEnableDirectMaterial(ChangeEvent<bool> evt)
		{
			if (this._materialObjectField != null)
			{
				this._materialObjectField.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
			}
			this.UpdateNailShapeFilter();
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
		}

		private Material? GetDirectMaterial()
		{
			if (this._enableDirectMaterial != null && !this._enableDirectMaterial.value) return null;
			return this._materialObjectField?.value as Material;
		}

		private void OnChangeMaterial(ChangeEvent<Object?> evt) { this.UpdateNailShapeFilter(); this.UpdatePreview(); this.RequestScenePreviewUpdate(); }
		private void OnChangeShapeDropDown(ChangeEvent<string> evt) { GlobalSetting.LastUseShapeName = evt.newValue; this.UpdatePreview(); this.RequestScenePreviewUpdate(); }
		private void OnChangeNailMaterialDropDown(ChangeEvent<string?> evt)
		{
			if (evt.newValue == null) return;
			foreach (var dd in this._nailDesignDropDowns!) dd.SetMaterialValue(evt.newValue);
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
		}
		private void OnChangeNailColorDropDown(ChangeEvent<string?> evt)
		{
			if (evt.newValue == null) return;
			foreach (var dd in this._nailDesignDropDowns!) dd.SetColorValue(evt.newValue);
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
		}
		private void OnChangeNailDesign(ChangeEvent<string?> evt)
		{
			if (evt.target is DropdownField { name: "NailDesignDropDowns-DesignDropDown" }) this.UpdateNailShapeFilter();
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
		}

		private void UpdateNailShapeFilter(INailProcessor? processor = null)
		{
			if (this.GetDirectMaterial() != null) { this._nailShapeDropDown!.SetFilter(_ => true); return; }
			if (processor != null) { this._nailShapeDropDown!.SetFilter(processor.IsSupportedNailShape); return; }

			HashSet<string> designNameSet = this._nailDesignDropDowns!
				.Select(downs => downs.GetSelectedDesignName())
				.Where(n => !string.IsNullOrEmpty(n))
				.ToHashSet();
			using DBNailDesign dbNailDesign = new();
			List<INailProcessor> processors = designNameSet.Select(INailProcessor.CreateNailDesign).ToList();
			if (processors.Count == 0) { this._nailShapeDropDown!.SetFilter(_ => true); return; }
			this._nailShapeDropDown!.SetFilter(shapeName => processors.All(p => p.IsSupportedNailShape(shapeName)));
		}

		private void UpdatePreview()
		{
			string nailShapeName = this._nailShapeDropDown?.value ?? GlobalSetting.LastUseShapeName ?? "oval";
			if (string.IsNullOrEmpty(nailShapeName)) nailShapeName = "oval";
			if (string.IsNullOrEmpty(nailShapeName)) return;

			Mesh?[]? overrideMeshes = this._nailShapeDropDown?.GetSelectedShapeMeshes();
			if (overrideMeshes == null) return;

			this._nailPreviewController?.ChangeNailShape(overrideMeshes);
			this._nailPreviewController?.ChangeFootNailMesh(nailShapeName);

			bool isHandActive = this._tglHandActive?.value ?? true;
			bool isFootActive = this._tglFootActive?.value ?? false;
			this._nailPreviewController?.UpdateVisibility(isHandActive, isFootActive);

			(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();

			Material? directMaterial = this.GetDirectMaterial();

			var perFingerAddMats = this.BuildPerFingerAdditionalMaterials(true);
			var perFingerAddObjs = this.BuildPerFingerAdditionalObjects(true);

			this._nailPreviewController?.ChangeNailMaterial(designAndVariationNames, nailShapeName, directMaterial,
				true, perFingerAddMats);
			this._nailPreviewController?.ChangeAdditionalObjects(designAndVariationNames, nailShapeName, perFingerAddObjs);
		}

		private void RequestScenePreviewUpdate()
		{
			if (!GlobalSetting.EnableSceneWearingPreview) return;

			this._scenePreviewSchedule?.Pause();
			this._scenePreviewSchedule = this.rootVisualElement.schedule
				.Execute(() => this.UpdateScenePreview(immediate: true))
				.StartingIn(SCENE_PREVIEW_DEBOUNCE_MS);
		}

		private void UpdateScenePreview(bool immediate)
		{
			if (!GlobalSetting.EnableSceneWearingPreview) return;

			var avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			if (avatar == null) return;

			var prefab = this._avatarDropDowns?.GetSelectedPrefab();
			if (prefab == null) return;

			string nailShapeName = this._nailShapeDropDown?.value ?? GlobalSetting.LastUseShapeName ?? "oval";
			if (string.IsNullOrEmpty(nailShapeName)) nailShapeName = "oval";
			if (string.IsNullOrEmpty(nailShapeName)) return;

			// BakeBS OFF: バリアント選択時はプレハブを丸ごと差し替え
			bool bakeBS = this._bakeBlendShapes?.value == true && this._forModularAvatar?.value == true;
			if (!bakeBS)
			{
				var bsPopup = this._avatarDropDowns?.BlendShapeVariantPopup;
				if (bsPopup != null && bsPopup.index > 0)
				{
					GameObject? variantPrefab = this.ResolveVariantPrefabForPreview(bsPopup.value);
					if (variantPrefab != null)
					{
						prefab = variantPrefab;
					}
				}
			}

			// シェイプ別 Prefab ([Oval]〜.prefab 等) を解決して着用と同じPrefabでプレビュー
			prefab = NailSetupProcessor.ResolveShapePrefab(prefab, nailShapeName);

			Mesh?[]? overrideMeshes = this._nailShapeDropDown?.GetSelectedShapeMeshes();
			if (overrideMeshes == null) overrideMeshes = new Mesh?[0];

			bool isHandActive = this._tglHandActive?.value ?? true;
			bool isFootActive = this._tglFootActive?.value ?? false;

			(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();
			Material? directMaterial = this.GetDirectMaterial();

			var perFingerAddMats = this.BuildPerFingerAdditionalMaterials(true);
			var perFingerAddObjs = this.BuildPerFingerAdditionalObjects(true);

			bool armatureCompensation = this._armatureScaleCompensation?.value == true;

			this._scenePreviewController ??= new MDNailScenePreviewController(SCENE_PREVIEW_NAME);
			this._scenePreviewController.Update(
				avatar,
				prefab,
				overrideMeshes,
				nailShapeName,
				isHandActive,
				isFootActive,
				designAndVariationNames,
				directMaterial,
				true,
				perFingerAddMats,
				perFingerAddObjs,
				armatureCompensation
			);

			// BakeBS ON: 体のBlendShape値に基づいてネイル位置を補間
			if (bakeBS)
			{
				this.ApplyVariantPositionBlend(avatar, prefab, nailShapeName);
			}
		}


		private void CleanupScenePreview()
		{
			VRCAvatarDescriptor? avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			// A-2 fix: Apply 直前の Cleanup で, Hide 状態の元 Renderer を強制的に enabled=true に戻す.
			// プレビュー Hide 状態のまま Apply に入ると元 Renderer が false のまま保存される事故を防ぐ保険.
			this._scenePreviewController?.ForceRestoreAllRenderers();
			this._scenePreviewController?.Cleanup(avatar);
			this._scenePreviewController = null;
		}

		/// <summary>
		/// BlendShapeバリアント名からバリアントプレハブを解決する（着用プレビュー用）。
		/// 選択中のネイルシェイプに対応するプレハブまで解決して返す。
		/// </summary>
		private GameObject? ResolveVariantPrefabForPreview(string variantName)
		{
			AvatarBlendShapeVariant[]? variants = this.GetBlendShapeVariants();
			if (variants == null) return null;

			AvatarBlendShapeVariant? variant = variants.FirstOrDefault(v => v.Name == variantName);
			if (variant == null || string.IsNullOrEmpty(variant.NailPrefabGUID)) return null;

			string path = AssetDatabase.GUIDToAssetPath(variant.NailPrefabGUID);
			if (string.IsNullOrEmpty(path) || MDNailToolAssetLoader.LoadPrefabSafe(path) == null)
			{
				ResourceAutoExtractor.EnsurePrefabExtractedByGuid(variant.NailPrefabGUID);
				AssetDatabase.Refresh();
				path = AssetDatabase.GUIDToAssetPath(variant.NailPrefabGUID);
			}
			if (string.IsNullOrEmpty(path)) return null;

			GameObject? variantPrefab = MDNailToolAssetLoader.LoadPrefabSafe(path);
			if (variantPrefab == null) return null;

			// シェイプ解決は呼び出し側 (UpdateScenePreview) で一元化。ここではベース variant Prefab のみ返す
			return variantPrefab;
		}

		/// <summary>
		/// BakeBS ON時: 体のBlendShape値に基づいてプレビューネイルの位置を補間する。
		/// 各バリアントの体BlendShape重みを読み取り、ベースとバリアントの位置デルタを加算する。
		/// </summary>
		private void ApplyVariantPositionBlend(VRCAvatarDescriptor avatar, GameObject basePrefab, string nailShapeName)
		{
			Transform? previewRoot = avatar.transform.Find(SCENE_PREVIEW_NAME);
			if (previewRoot == null) return;

			AvatarBlendShapeVariant[]? variants = this.GetBlendShapeVariants();
			if (variants == null || variants.Length == 0) return;

			var previewTransforms = previewRoot.GetComponentsInChildren<Transform>(true);
			var baseTransforms = basePrefab.GetComponentsInChildren<Transform>(true);

			foreach (var variant in variants)
			{
				if (string.IsNullOrEmpty(variant.SyncSourceSmrName)) continue;

				// 体のSMRからBlendShape重みを取得
				float weight = GetBodyBlendShapeWeight(avatar, variant);
				if (weight <= 0f) continue;

				// バリアントプレハブをロード
				if (string.IsNullOrEmpty(variant.NailPrefabGUID)) continue;
				string varPath = AssetDatabase.GUIDToAssetPath(variant.NailPrefabGUID);
				if (string.IsNullOrEmpty(varPath)) continue;
				GameObject? variantPrefab = MDNailToolAssetLoader.LoadPrefabSafe(varPath);
				if (variantPrefab == null) continue;

				variantPrefab = NailSetupProcessor.ResolveShapePrefab(variantPrefab, nailShapeName);
				var variantTransforms = variantPrefab.GetComponentsInChildren<Transform>(true);

				// 各ネイルの位置・回転デルタを補間適用
				foreach (Transform previewNail in previewTransforms)
				{
					if (previewNail == previewRoot) continue;

					Transform? baseNail = System.Array.Find(baseTransforms, t => t.name == previewNail.name);
					Transform? variantNail = System.Array.Find(variantTransforms, t => t.name == previewNail.name);
					if (baseNail == null || variantNail == null) continue;

					Vector3 posDelta = variantNail.localPosition - baseNail.localPosition;
					previewNail.localPosition += weight * posDelta;

					Quaternion rotDelta = variantNail.localRotation * Quaternion.Inverse(baseNail.localRotation);
					previewNail.localRotation = Quaternion.Slerp(Quaternion.identity, rotDelta, weight) * previewNail.localRotation;
				}
			}
		}

		/// <summary>バリアント一覧を取得する共通メソッド</summary>
		private AvatarBlendShapeVariant[]? GetBlendShapeVariants()
		{
			var avatarVariationData = this._avatarDropDowns?.GetSelectedAvatarVariation();
			if (avatarVariationData == null) return null;

			AvatarBlendShapeVariant[]? variants = avatarVariationData.BlendShapeVariants;
			if (variants == null)
			{
				using DBShop dbShop = new();
				string avatarName = this._avatarDropDowns!.GetAvatarName();
				foreach (Shop s in dbShop.collection)
				{
					Avatar? av = s.FindAvatarByName(avatarName);
					if (av?.BlendShapeVariants != null)
					{
						variants = av.BlendShapeVariants;
						break;
					}
				}
			}
			return variants;
		}

		/// <summary>体のSMRからバリアントに対応するBlendShapeの重み（0〜1）を取得する</summary>
		private static float GetBodyBlendShapeWeight(VRCAvatarDescriptor avatar, AvatarBlendShapeVariant variant)
		{
			Transform? srcSmrTransform = FindSyncSourceSmr(avatar, variant.SyncSourceSmrName!);
			if (srcSmrTransform == null) return 0f;

			SkinnedMeshRenderer? srcSmr = srcSmrTransform.GetComponent<SkinnedMeshRenderer>();
			if (srcSmr == null || srcSmr.sharedMesh == null) return 0f;

			string normalizedName = variant.Name.Replace(" ", "").Replace("　", "");
			for (int i = 0; i < srcSmr.sharedMesh.blendShapeCount; i++)
			{
				string bsName = srcSmr.sharedMesh.GetBlendShapeName(i);
				if (bsName.Replace(" ", "").Replace("　", "") == normalizedName)
				{
					return srcSmr.GetBlendShapeWeight(i) / 100f;
				}
			}
			return 0f;
		}

		/// <summary>SyncSourceSmrNameからアバター上のSMR Transformを検索する（NailSetupProcessorと同じフォールバックロジック）</summary>
		private static Transform? FindSyncSourceSmr(VRCAvatarDescriptor avatar, string syncSourceSmrName)
		{
			var allTransforms = avatar.transform.GetComponentsInChildren<Transform>(true);

			// Step 1: 名前完全一致
			Transform? t = System.Array.Find(allTransforms, tr => tr.name == syncSourceSmrName);
			if (t != null) return t;

			// Step 2: 大文字小文字無視
			t = System.Array.Find(allTransforms, tr => string.Equals(tr.name, syncSourceSmrName, System.StringComparison.OrdinalIgnoreCase));
			if (t != null) return t;

			// Step 3: 部分一致
			t = System.Array.Find(allTransforms, tr => tr.GetComponent<SkinnedMeshRenderer>() != null
				&& (tr.name.Contains(syncSourceSmrName) || syncSourceSmrName.Contains(tr.name)));
			if (t != null) return t;

			// Step 4: BlendShapeを持つ非顔SMRから推測
			SkinnedMeshRenderer? visemeSmr = avatar.VisemeSkinnedMesh;
			return avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
				.Where(smr => smr != visemeSmr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
				.OrderByDescending(smr => smr.sharedMesh!.blendShapeCount)
				.FirstOrDefault()?.transform;
		}
	}
}

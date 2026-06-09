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
		private void BindActions()
		{
			this._execute = this.rootVisualElement.Q<LocalizedButton>("execute");
			this._remove = this.rootVisualElement.Q<LocalizedButton>("remove");

			this._execute.clicked += this.OnExecute;
			this._remove.clicked += this.OnRemove;
		}

		private void PostInitSelection()
		{
			var nailDesignSelect = this._nailDesignSelect;
			if (nailDesignSelect != null && nailDesignSelect.FirstDesignName != null)
			{
				this.OnSelectNail(nailDesignSelect.FirstDesignName);
			}
			else
			{
				// インストール済みネイルデザインがない場合、マテリアル直接指定を自動ON
				if (this._enableDirectMaterial != null)
				{
					this._enableDirectMaterial.value = true;
				}

				this.UpdatePreview();
				this.RequestScenePreviewUpdate();
			}

			// Selection.activeGameObject からアバターを検出
			VRCAvatarDescriptor? descriptor = null;
			if (Selection.activeGameObject != null)
			{
				descriptor = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>();
			}

			// 見つからなかった場合、Hierarchy全体をスキャンして1体だけなら自動設定
			if (descriptor == null)
			{
				VRCAvatarDescriptor[] allDescriptors = Object.FindObjectsByType<VRCAvatarDescriptor>(
					FindObjectsInactive.Exclude,
					FindObjectsSortMode.None);
				if (allDescriptors.Length == 1)
				{
					descriptor = allDescriptors[0];
				}
			}

			if (descriptor != null)
			{
				var avatarObjectField = this._avatarObjectField;
				if (avatarObjectField != null)
				{
					avatarObjectField.value = descriptor;
				}

				AvatarMatching avatarMatching = new(descriptor);
				(Shop shop, Entity.Avatar avatar, AvatarVariation variation)? variation = avatarMatching.Match();
				if (variation != null && this._avatarDropDowns != null)
				{
					this._avatarDropDowns.SetValues(variation.Value.shop, variation.Value.avatar, variation.Value.variation);
				}
			}

			this.UpdateBlendShapeVariantDropDown();
		}


		private void SetupFootVisualElements()
		{
			var footSelects = this.rootVisualElement.Q<VisualElement>("foot-selects");
			this._footSelects = footSelects;

			if (footSelects == null) return;

			footSelects.Clear();

			string[] toeTextIds = { "window.thumb", "window.index_finger", "window.middle_finger", "window.ring_finger", "window.little_finger" };

			// ---- 左足ヘッダー行（ハンドネイルと同じ構造） ----
			var leftHeader = new VisualElement();
			leftHeader.AddToClassList("mdn-finger-header");

			var bulkLeftFoot = new Toggle { name = "bulk-left-foot" };
			bulkLeftFoot.AddToClassList("mdn-bulk-toggle");
			leftHeader.Add(bulkLeftFoot);

			var leftFootColLabel = new LocalizedLabel { TextId = "window.left_foot" };
			leftFootColLabel.AddToClassList("mdn-finger-name-col");
			leftFootColLabel.AddToClassList("mdn-col-header");
			leftHeader.Add(leftFootColLabel);

			var leftDesignHeader = new LocalizedLabel { TextId = "window.nail_design" };
			leftDesignHeader.AddToClassList("mdn-finger-design-col");
			leftDesignHeader.AddToClassList("mdn-col-header");
			leftHeader.Add(leftDesignHeader);

			var leftMatHeader = new LocalizedLabel { TextId = "window.nail_material" };
			leftMatHeader.AddToClassList("mdn-finger-mat-col");
			leftMatHeader.AddToClassList("mdn-col-header");
			leftHeader.Add(leftMatHeader);

			var leftColHeader = new LocalizedLabel { TextId = "window.nail_color" };
			leftColHeader.AddToClassList("mdn-finger-col-col");
			leftColHeader.AddToClassList("mdn-col-header");
			leftHeader.Add(leftColHeader);

			var leftAddMatHeader = new LocalizedLabel { TextId = "window.additional_material" };
			leftAddMatHeader.AddToClassList("mdn-finger-addmat-col");
			leftAddMatHeader.AddToClassList("mdn-col-header");
			leftHeader.Add(leftAddMatHeader);

			var leftAddObjHeader = new LocalizedLabel { TextId = "window.additional_object" };
			leftAddObjHeader.AddToClassList("mdn-finger-addobj-col");
			leftAddObjHeader.AddToClassList("mdn-col-header");
			leftHeader.Add(leftAddObjHeader);

			footSelects.Add(leftHeader);

			// ---- 左足 5本 ----
			string[] leftToes = { "left-foot-thumb", "left-foot-index", "left-foot-middle", "left-foot-ring", "left-foot-little" };
			for (int i = 0; i < 5; i++)
			{
				var dd = new NailDesignDropDowns { name = leftToes[i] };
				dd.SetFingerName(toeTextIds[i]);
				footSelects.Add(dd);
			}

			// ---- 右足区切り行 ----
			var divider = new VisualElement();
			divider.AddToClassList("mdn-hand-divider");

			var bulkRightFoot = new Toggle { name = "bulk-right-foot" };
			bulkRightFoot.AddToClassList("mdn-bulk-toggle");
			divider.Add(bulkRightFoot);

			var rightFootDivLabel = new LocalizedLabel { TextId = "window.right_foot" };
			rightFootDivLabel.AddToClassList("mdn-finger-name-col");
			rightFootDivLabel.AddToClassList("mdn-col-header");
			divider.Add(rightFootDivLabel);

			footSelects.Add(divider);

			// ---- 右足 5本 ----
			string[] rightToes = { "right-foot-thumb", "right-foot-index", "right-foot-middle", "right-foot-ring", "right-foot-little" };
			for (int i = 0; i < 5; i++)
			{
				var dd = new NailDesignDropDowns { name = rightToes[i] };
				dd.SetFingerName(toeTextIds[i]);
				footSelects.Add(dd);
			}
		}

		private void InitializeNailDesignDropDowns()
		{
			var allDropdowns = new NailDesignDropDowns?[] {
				this.rootVisualElement.Q<NailDesignDropDowns>("left-thumb"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-index"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-middle"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-ring"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-little"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-thumb"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-index"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-middle"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-ring"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-little"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-foot-thumb"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-foot-index"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-foot-middle"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-foot-ring"),
				this.rootVisualElement.Q<NailDesignDropDowns>("left-foot-little"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-foot-thumb"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-foot-index"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-foot-middle"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-foot-ring"),
				this.rootVisualElement.Q<NailDesignDropDowns>("right-foot-little")
			};

			// null除去前に指インデックスを設定（配列インデックス = 指インデックス）
			for (int i = 0; i < allDropdowns.Length; i++)
				allDropdowns[i]?.SetFingerIndex(i);

			this._nailDesignDropDowns = allDropdowns.Where(d => d != null).ToArray()!;

			foreach (NailDesignDropDowns nailDesignDropDown in this._nailDesignDropDowns)
			{
				nailDesignDropDown.RegisterCallback<ChangeEvent<string?>>(this.OnChangeNailDesign);
				nailDesignDropDown.OnFingerEnabledChanged += () => {
					this.UpdateNailShapeFilter();
					this.UpdatePreview();
					this.RequestScenePreviewUpdate();
					this.UpdateAllBulkToggleStates();
				};
			}

			this._bulkLeftHand = this.rootVisualElement.Q<Toggle>("bulk-left-hand");
			this._bulkRightHand = this.rootVisualElement.Q<Toggle>("bulk-right-hand");
			this._bulkLeftFoot = this.rootVisualElement.Q<Toggle>("bulk-left-foot");
			this._bulkRightFoot = this.rootVisualElement.Q<Toggle>("bulk-right-foot");

			this._bulkLeftHand?.RegisterValueChangedCallback(evt => this.SetBulkFingers(0, 5, evt.newValue));
			this._bulkRightHand?.RegisterValueChangedCallback(evt => this.SetBulkFingers(5, 5, evt.newValue));
			this._bulkLeftFoot?.RegisterValueChangedCallback(evt => this.SetBulkFingers(10, 5, evt.newValue));
			this._bulkRightFoot?.RegisterValueChangedCallback(evt => this.SetBulkFingers(15, 5, evt.newValue));

			var lblBulkLeftHand = this._bulkLeftHand?.parent?.Q<LocalizedLabel>(className: "mdn-finger-name-col");
			var lblBulkRightHand = this._bulkRightHand?.parent?.Q<LocalizedLabel>(className: "mdn-finger-name-col");
			var lblBulkLeftFoot = this._bulkLeftFoot?.parent?.Q<LocalizedLabel>(className: "mdn-finger-name-col");
			var lblBulkRightFoot = this._bulkRightFoot?.parent?.Q<LocalizedLabel>(className: "mdn-finger-name-col");
			lblBulkLeftHand?.RegisterCallback<ClickEvent>(_ => { if (this._bulkLeftHand != null) this._bulkLeftHand.value = !this._bulkLeftHand.value; });
			lblBulkRightHand?.RegisterCallback<ClickEvent>(_ => { if (this._bulkRightHand != null) this._bulkRightHand.value = !this._bulkRightHand.value; });
			lblBulkLeftFoot?.RegisterCallback<ClickEvent>(_ => { if (this._bulkLeftFoot != null) this._bulkLeftFoot.value = !this._bulkLeftFoot.value; });
			lblBulkRightFoot?.RegisterCallback<ClickEvent>(_ => { if (this._bulkRightFoot != null) this._bulkRightFoot.value = !this._bulkRightFoot.value; });

			this.UpdateAllBulkToggleStates();
		}

		private void SetBulkFingers(int startIdx, int count, bool enabled)
		{
			if (this._nailDesignDropDowns == null) return;
			foreach (var dd in this._nailDesignDropDowns)
			{
				int fi = dd.GetFingerIndex();
				if (fi >= startIdx && fi < startIdx + count) dd.SetFingerEnabledExternal(enabled);
			}
			this.UpdateNailShapeFilter();
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
			this.UpdateAllBulkToggleStates();
		}

		private void UpdateAllBulkToggleStates()
		{
			this.UpdateBulkToggleState(0, 5, this._bulkLeftHand);
			this.UpdateBulkToggleState(5, 5, this._bulkRightHand);
			this.UpdateBulkToggleState(10, 5, this._bulkLeftFoot);
			this.UpdateBulkToggleState(15, 5, this._bulkRightFoot);
		}

		private void UpdateBulkToggleState(int startIdx, int count, Toggle? bulk)
		{
			if (bulk == null || this._nailDesignDropDowns == null) return;
			bool allEnabled = this._nailDesignDropDowns
				.Where(dd => { int fi = dd.GetFingerIndex(); return fi >= startIdx && fi < startIdx + count; })
				.All(dd => dd.IsFingerEnabled);
			bulk.SetValueWithoutNotify(allEnabled);
		}

		private void InitializeHandFootControl()
		{
			this._tglHandActive = this.rootVisualElement.Q<Toggle>("toggle-hand-active");
			this._tglHandDetail = this.rootVisualElement.Q<Toggle>("toggle-hand-detail");
			var lblHandActive = this.rootVisualElement.Q<Label>("label-hand-active");
			var lblHandDetail = this.rootVisualElement.Q<Label>("label-hand-detail");

			this._tglFootActive = this.rootVisualElement.Q<Toggle>("toggle-foot-active");
			this._tglFootDetail = this.rootVisualElement.Q<Toggle>("toggle-foot-detail");
			var lblFootActive = this.rootVisualElement.Q<Label>("label-foot-active");
			var lblFootDetail = this.rootVisualElement.Q<Label>("label-foot-detail");

			this._handSelects = this.rootVisualElement.Q<VisualElement>("hand-selects");
			if (this._footSelects == null) this._footSelects = this.rootVisualElement.Q<VisualElement>("foot-selects");

			// ハンド/フットセクションヘッダーを保存（エラーハイライト用）
			this._handSectionHeader = this._tglHandActive?.parent;
			this._footSectionHeader = this._tglFootActive?.parent;

			if (_tglHandActive != null && _tglHandDetail != null)
			{
				_tglHandActive.SetValueWithoutNotify(MDNailToolPrefs.HandActive);
				_tglHandDetail.SetValueWithoutNotify(MDNailToolPrefs.HandDetail);

				lblHandActive?.RegisterCallback<ClickEvent>(_ => _tglHandActive.value = !_tglHandActive.value);
				lblHandDetail?.RegisterCallback<ClickEvent>(_ => _tglHandDetail.value = !_tglHandDetail.value);

				void UpdateHandVisiblity()
				{
					if (this._handSelects == null) return;
					bool isActive = _tglHandActive.value;
					bool isDetail = _tglHandDetail.value;

					this._handSelects.style.display = (isActive && isDetail) ? DisplayStyle.Flex : DisplayStyle.None;
					_tglHandDetail.SetEnabled(isActive);

					MDNailToolPrefs.HandActive = isActive;
					MDNailToolPrefs.HandDetail = isDetail;

					this.UpdatePreview();
					this.RequestScenePreviewUpdate();

					// どちらかがONになったらエラーハイライトとバナーを解除
					if (isActive || (this._tglFootActive?.value ?? false))
					{
						this.ClearHandFootError();
						if (this._errorMessage?.text == S("error.execute.no_target"))
							this.HideErrorBanner();
					}
				}

				_tglHandActive.RegisterValueChangedCallback(_ => UpdateHandVisiblity());
				_tglHandDetail.RegisterValueChangedCallback(_ => UpdateHandVisiblity());
				UpdateHandVisiblity();
			}

			if (_tglFootActive != null && _tglFootDetail != null)
			{
				_tglFootActive.SetValueWithoutNotify(GlobalSetting.UseFootNail);
				_tglFootDetail.SetValueWithoutNotify(MDNailToolPrefs.FootDetail);

				lblFootActive?.RegisterCallback<ClickEvent>(_ => _tglFootActive.value = !_tglFootActive.value);
				lblFootDetail?.RegisterCallback<ClickEvent>(_ => _tglFootDetail.value = !_tglFootDetail.value);

				void UpdateFootVisibility()
				{
					if (this._footSelects == null) return;
					bool isActive = _tglFootActive.value;
					bool isDetail = _tglFootDetail.value;

					this._footSelects.style.display = (isActive && isDetail) ? DisplayStyle.Flex : DisplayStyle.None;
					_tglFootDetail.SetEnabled(isActive);

					GlobalSetting.UseFootNail = isActive;
					MDNailToolPrefs.FootDetail = isDetail;

					this.UpdatePreview();
					this.RequestScenePreviewUpdate();

					// どちらかがONになったらエラーハイライトとバナーを解除
					if (isActive || (this._tglHandActive?.value ?? false))
					{
						this.ClearHandFootError();
						if (this._errorMessage?.text == S("error.execute.no_target"))
							this.HideErrorBanner();
					}
				}

				_tglFootActive.RegisterValueChangedCallback(_ => UpdateFootVisibility());
				_tglFootDetail.RegisterValueChangedCallback(_ => UpdateFootVisibility());
				UpdateFootVisibility();
			}
		}

		private void OnChangeAvatarSortOrder(AvatarSortOrder order) { this._avatarDropDowns?.Sort(order); }

		private void OnChangeAvatar(ChangeEvent<Object> evt)
		{
			if (evt.newValue is VRCAvatarDescriptor avatar)
			{
				// アバターが設定されたらエラー枠・バナーを解除
				this.ClearAvatarFieldError();
				this.HideErrorBanner();

				AvatarMatching matching = new(avatar);
				(Shop shop, Entity.Avatar avatar, AvatarVariation variation)? result = matching.Match();
				if (result != null)
				{
					this._avatarDropDowns!.SetValues(result.Value.shop, result.Value.avatar, result.Value.variation);
					this.UpdateBlendShapeVariantDropDown();
				}

				this.CleanupScenePreview();
				this.UpdatePreview();
				this.RequestScenePreviewUpdate();
			}
		}

		private void OnDestroy()
		{
			INailProcessor.ClearPreviewMaterialCash();
			this.CleanupScenePreview();
			ToolConsole.OnLog = null;
		}

		private world.anlabo.mdnailtool.Editor.Window.Controllers.MDNailScenePreviewController? _scenePreviewController;


		private void OnExecute()
		{
			this.CleanupScenePreview();
			this._nailPreviewController?.CleanupAdditionalObjects();

			// 試着プレビューを解除 (確定適用するのでバナーとボタン見た目を戻す)
			if (this._tryoutActive)
			{
				this._tryoutActive = false;
				GlobalSetting.EnableSceneWearingPreview = false;
				this.UpdateTryoutVisual();
			}
			this.HideErrorBanner();

			// ---- Validation ----
			VRCAvatarDescriptor? avatar = this._avatarObjectField!.value as VRCAvatarDescriptor;
			if (avatar == null)
			{
				this.ShowErrorBanner(S("dialog.error.select_target_avatar"));
				this.ShowAvatarFieldError();
				return;
			}
			this.ClearAvatarFieldError();

			bool isHandActive = this._tglHandActive?.value ?? true;
			bool isFootActive = this._tglFootActive?.value ?? false;

			if (!isHandActive && !isFootActive)
			{
				this.ShowErrorBanner(S("error.execute.no_target"));
				this.ShowHandFootError();
				return;
			}
			this.ClearHandFootError();

			AvatarVariation? avatarVariationData = this._avatarDropDowns!.GetSelectedAvatarVariation();
			if (avatarVariationData == null)
			{
				this.ShowErrorBanner(S("error.execute.no_avatar_variation"));
				return;
			}

			GameObject? prefab = this._avatarDropDowns!.GetSelectedPrefab();
			if (prefab == null)
			{
				this.ShowErrorBanner(S("error.execute.no_prefab"));
				return;
			}

			string? nailShapeName = this._nailShapeDropDown!.value;
			if (nailShapeName == null)
			{
				this.ShowErrorBanner(S("error.execute.no_nail_shape"));
				return;
			}

			// ---- Process ----
			bool assetEditingStarted = false;
			try
			{
				// StartAssetEditing 外で呼ぶ (バッチ中はシーン保存が書込失敗するため)
				if (this._backup!.value)
				{
					try
					{
						NailSetupProcessor.CreateBackup(avatar.gameObject);
					}
					catch (Exception e)
					{
						ToolConsole.Warn("Backup", $"Backup creation failed. Continue setup without backup.\n{e}");
					}
				}

				AssetDatabase.StartAssetEditing();
				assetEditingStarted = true;

				(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();

				Mesh?[]? selectedMeshes = this._nailShapeDropDown!.GetSelectedShapeMeshes();
				Mesh?[]? overrideMesh = isHandActive ? selectedMeshes : new Mesh?[10];

				Material? directMaterial = this.GetDirectMaterial();

				NailSetupProcessor processor = new(avatar, avatarVariationData, prefab, designAndVariationNames, nailShapeName)
				{
					AvatarName = this._avatarDropDowns?.GetAvatarName(),
					OverrideMesh = overrideMesh,
					UseFootNail = isFootActive,
					RemoveCurrentNail = this._removeCurrentNail!.value,
					GenerateMaterial = directMaterial == null,  // 直接指定時は生成OFF
					Backup = this._backup!.value,
					ForModularAvatar = this._forModularAvatar!.value,
					OverrideMaterial = directMaterial,
					GenerateExpressionMenu = (this._forModularAvatar?.value == true)
					                      && (this._generateExpressionMenu?.value == true),
					SplitHandFoot = (this._forModularAvatar?.value == true)
					             && (this._generateExpressionMenu?.value == true)
					             && (this._splitHandFootExpressionMenu?.value == true),
					MergeAnLabo = (this._forModularAvatar?.value == true)
					           && (this._generateExpressionMenu?.value == true)
					           && (this._mergeAnLaboExpressionMenu?.value == true),
					ArmatureScaleCompensation = (this._armatureScaleCompensation?.value == true),
					BakeBlendShapes = (this._forModularAvatar?.value == true)
					               && (this._bakeBlendShapes?.value == true),
					SyncBlendShapesWithMA = (this._forModularAvatar?.value == true)
					                     && (this._bakeBlendShapes?.value == true)
					                     && (this._syncBlendShapesWithMA?.value ?? true),
					SelectedBlendShapeVariantName = (!(this._forModularAvatar?.value == true && this._bakeBlendShapes?.value == true) && this._avatarDropDowns?.BlendShapeVariantPopup != null && this._avatarDropDowns.BlendShapeVariantPopup.index > 0) ? this._avatarDropDowns.BlendShapeVariantPopup.value : null,
					EnablePenetrationCorrection = (this._penetrationCorrection?.value == true),
					EnableAdditionalMaterials = true,
					PerFingerAdditionalMaterials = this.BuildPerFingerAdditionalMaterials(false),
					PerFingerAdditionalObjects = this.BuildPerFingerAdditionalObjects(false),
				};

				// AvatarEntityをprocessorにセット（shop.jsonのblendShapeVariantsを参照するため）
				{
					using DBShop dbShop = new();
					string avatarName = this._avatarDropDowns!.GetAvatarName();
					foreach (Shop s in dbShop.collection)
					{
						Avatar? av = s.FindAvatarByName(avatarName);
						if (av != null)
						{
							processor.AvatarEntity = av;
							break;
						}
					}
				}

				processor.Process();

				if (!isHandActive) this.RemoveHandNailObjects(avatar);
				if (!isFootActive) this.RemoveFootNailObjects(avatar);

				string avatarKey = this._avatarDropDowns!.GetAvatarKey();
				MDNailToolUsageStats.Update(designAndVariationNames, avatarKey);
				this._nailDesignSelect!.Init();

				this.HideErrorBanner();
				if (processor.Warnings.Count > 0)
				{
					string warnSummary = string.Format(
						S("warning.variant_load_failed") ?? "一部のバリアントの読み込みに失敗しました ({0}件)",
						processor.Warnings.Count);
					this.ShowWarningBanner(warnSummary, processor.Warnings);
				}
				else
				{
					this.HideWarningBanner();
				}
				string successMessage = BuildSuccessMessage(isHandActive, isFootActive);
				EditorUtility.DisplayDialog(S("dialog.finished"), successMessage, "OK");

				// 着用時にウィンドウを閉じる (チェックOFFなら何もしない)
				if (GlobalSetting.CloseWindowOnExecute)
				{
					this.Close();
					return;
				}
			}
			catch (NailSetupUserException e)
			{
				ToolConsole.Warn("Window", $"{e.Message}\n{e.StackTrace}");
				this.ShowErrorBanner(e.Message);
				this._userErrorCount++;
				if (this._userErrorCount >= 2) {
					this.ShowContactLinks(e.ToString());
				}
			}
			catch (NailToolUserException e)
			{
				ToolConsole.Warn("Window", $"{e.Message}\n{e.StackTrace}");
				this.ShowErrorBanner(e.Message);
				this._userErrorCount++;
				if (this._userErrorCount >= 2) {
					this.ShowContactLinks(e.ToString());
				}
			}
			catch (Exception e)
			{
				ToolConsole.Error("Window", $"{e}");
				this.ShowErrorBanner(S("error.execute.unexpected"), e);
				this.ShowContactLinks(e.ToString());
			}
			finally
			{
				if (assetEditingStarted) AssetDatabase.StopAssetEditing();
				AssetDatabase.Refresh();
			}
		}

		private static string BuildSuccessMessage(bool handActive, bool footActive)
		{
			string handLabel = S("window.hand_nail") ?? "Hand Nail";
			string footLabel = S("window.foot_nail") ?? "Foot Nail";
			string suffix    = S("dialog.finished.attached_suffix") ?? " attachment is complete.";

			string target = (handActive, footActive) switch {
				(true,  true)  => handLabel + " " + (S("dialog.finished.and") ?? "&") + " " + footLabel,
				(true,  false) => handLabel,
				(false, true)  => footLabel,
				_              => ""
			};
			return target + suffix;
		}

		private void RemoveHandNailObjects(VRCAvatarDescriptor avatar)
		{
			foreach (string nailName in MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST)
			{
				var targets = avatar.transform.GetComponentsInChildren<Transform>(true)
					.Where(t => t.name.Contains(nailName)).ToArray();
				foreach (var t in targets) if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
			}
		}

		private void RemoveFootNailObjects(VRCAvatarDescriptor avatar)
		{
			var targetNames = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
				.Concat(MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST);
			foreach (string nailName in targetNames)
			{
				var targets = avatar.transform.GetComponentsInChildren<Transform>(true)
					.Where(t => t.name.Contains(nailName)).ToArray();
				foreach (var t in targets) if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
			}
		}

		private void OnRemove()
		{
			this.CleanupScenePreview();
			this.HideErrorBanner();
			this.HideWarningBanner();

			VRCAvatarDescriptor? avatar = this._avatarObjectField!.value as VRCAvatarDescriptor;
			if (avatar == null)
			{
				this.ShowErrorBanner(S("dialog.error.select_target_avatar"));
				this.ShowAvatarFieldError();
				return;
			}
			this.ClearAvatarFieldError();

			AvatarVariation? avatarVariationData = this._avatarDropDowns!.GetSelectedAvatarVariation();
			if (avatarVariationData != null) NailSetupProcessor.RemoveNail(avatar, avatarVariationData.BoneMappingOverride);
		}


		private (INailProcessor, string, string)[] GetNailProcessors()
		{
			return MDNailSelectionBuilder.Build(
				this._nailDesignDropDowns!,
				this._tglHandActive?.value ?? true,
				this._tglHandDetail?.value ?? false,
				this._tglFootActive?.value ?? false,
				this._tglFootDetail?.value ?? false
			);
		}
	}
}

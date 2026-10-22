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
			if (nailDesignSelect != null && nailDesignSelect.LatestInstalledDesignName != null)
			{
				this.OnSelectNail(nailDesignSelect.LatestInstalledDesignName);
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

			// Initial selection and later ObjectField changes use the same reset/match path.
			this._avatarObjectField?.SetValueWithoutNotify(descriptor);
			this.RefreshAvatarSelection(descriptor);

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

		private bool _avatarSelectionNeedsManual;

		private void OnChangeAvatar(ChangeEvent<Object> evt)
		{
			this.RefreshAvatarSelection(evt.newValue as VRCAvatarDescriptor);
			this.RefreshMultiNailMenuPreview();
		}

		private void RefreshAvatarSelection(VRCAvatarDescriptor? avatar)
		{
			// Clear the old selection before matching, including when matching fails or throws.
			this.CleanupScenePreview();
			this._avatarDropDowns?.ClearSelection();
			this._avatarSelectionNeedsManual = avatar != null;
			this.ClearAvatarFieldError();
			this.HideErrorBanner();

			if (avatar != null)
			{
				(Shop shop, Entity.Avatar avatar, AvatarVariation variation)? result = null;
				try
				{
					result = new AvatarMatching(avatar).Match();
				}
				catch (Exception ex)
				{
					ToolConsole.Warn("Avatar", $"Automatic avatar matching failed: {ex}");
				}
				if (result != null && this._avatarDropDowns != null)
				{
					this._avatarDropDowns.SetValues(result.Value.shop, result.Value.avatar, result.Value.variation);
					this._avatarSelectionNeedsManual = false;
				}
				else
				{
					this.ShowErrorBanner(S("error.avatar.manual_selection_required"));
				}
			}

			this.UpdateBlendShapeVariantDropDown();
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
			this.UpdateStepSectionStates();
		}

		private void CompleteManualAvatarSelection()
		{
			if (!this._avatarSelectionNeedsManual || this._avatarDropDowns?.GetSelectedAvatarVariation() == null) return;
			this._avatarSelectionNeedsManual = false;
			this.HideErrorBanner();
		}

		// Release the owned preview before a domain reload drops the controller reference.
		private void OnDisable()
		{
			this.CleanupScenePreview();
		}

		private void OnDestroy()
		{
			try
			{
				ToolConsole.SetSupportInfoSnapshot(this.BuildConsoleDiagnosticInfo());
			}
			catch (Exception ex)
			{
				ToolConsole.Warn("NailTool", $"問い合わせ情報の保持に失敗: {ex.Message}");
			}
			FAQWindow.CloseAll();
			INailProcessor.ClearPreviewMaterialCash();
			this.CleanupScenePreview();
			ToolConsole.OnLog = null;
		}

		private world.anlabo.mdnailtool.Editor.Window.Controllers.MDNailScenePreviewController? _scenePreviewController;


		private void OnExecute()
		{
			bool isMultiNail = this.IsMultiNailMode;
			if (!isMultiNail && this._enableDirectMaterial?.value == true && this._materialObjectField?.value == null)
			{
				this.ShowErrorBanner(S("error.custom_nail.material_required"));
				return;
			}
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

			DBShop.ClearCache();
			AvatarVariation? avatarVariationData = this._avatarDropDowns!.GetSelectedAvatarVariation();
			if (avatarVariationData == null)
			{
				this.ShowErrorBanner(S(this._avatarSelectionNeedsManual ? "error.avatar.manual_selection_required" : "error.execute.no_avatar_variation"));
				return;
			}

			// まとめて登録で自動ONにする設定を先に確定させ、後続の設定チェックがそれを前提に判定できるようにする。
			var multiSelections = isMultiNail ? this.GetMultiNailSelections() : null;
			if (multiSelections != null)
			{
				if (multiSelections.Count == 0)
				{
					this.ShowErrorBanner(S("error.multi_nail.no_selection") ?? "Select at least one nail to register.");
					return;
				}
#if MD_NAIL_FOR_MA
				this.ForceMultiNailMenuSettings();
#else
				this.ShowErrorBanner(S("error.multi_nail.requires_ma") ?? "Switch registration needs Modular Avatar installed in the project.");
				return;
#endif
			}

			// Body表面ウェイト転送は、MAの結合SMRを生成する経路でのみ実行できる。
			if (NailSetupUtil.ParseWeightTransferFingers(avatarVariationData.WeightTransfer).Count > 0
				&& (this._forModularAvatar?.value != true || this._bakeBlendShapes?.value != true))
			{
				this.ShowErrorBanner(S("error.execute.weight_transfer_requires_ma_combine"), showRequiredSettingsAction: true);
				return;
			}

			using var temporaryPrefabs = NailPrefabBuilder.BeginTemporaryScope();
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
			// 形ごとにマテリアルがあるため、どの形にも対応しないネイルは付けられない。付け始める前に止める。
			if (multiSelections != null)
			{
				string? noShape;
				try
				{
					List<string> shapeNames;
					using (DBNailShape dbNailShape = new()) shapeNames = dbNailShape.collection.Select(s => s.ShapeName).ToList();
					noShape = multiSelections.Select(s => s.designName).Distinct()
						.FirstOrDefault(d => !shapeNames.Any(INailProcessor.CreateNailDesign(d).IsSupportedNailShape));
				}
				catch (Exception e)
				{
					// デザインの読み込み失敗も、着用中の失敗と同じ案内で知らせる。
					ToolConsole.Error("Window", $"{e}");
					this.ShowErrorBanner(S("error.execute.unexpected"), e);
					this.ShowContactLinks(e.ToString());
					return;
				}
				if (noShape != null)
				{
					this.ShowErrorBanner(string.Format(S("error.multi_nail.no_shape") ?? "No nail shape is available for {0}.", noShape));
					return;
				}
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
						ToolConsole.Warn("Backup", $"Backup creation failed. Setup stopped.\n{e}");
						this.ShowErrorBanner(S("error.execute.backup_failed"), e);
						return;
					}
				}

				AssetDatabase.StartAssetEditing();
				assetEditingStarted = true;

				Mesh?[]? selectedMeshes = this._nailShapeDropDown!.GetSelectedShapeMeshes();
				Mesh?[]? overrideMesh = isHandActive ? selectedMeshes : new Mesh?[10];

				Material? directMaterial = isMultiNail ? null : this.GetDirectMaterial();

				Avatar? freshAvatarEntity = null;
				DBShop.ClearCache();
				using (DBShop dbShop = new())
				{
					string shopName = this._avatarDropDowns!.GetShopName();
					string avatarName = this._avatarDropDowns!.GetAvatarName();
					Shop? selectedShop = dbShop.FindShopByName(shopName);
					Avatar? av = selectedShop?.FindAvatarByName(avatarName);
					if (av != null)
					{
						freshAvatarEntity = av;
						AvatarVariation? freshVariation = av.FindAvatarVariation(avatarVariationData.VariationName);
						if (freshVariation != null) avatarVariationData = freshVariation;
					}
				}

				// 着用処理は一時プレハブを使い終わると破棄するため、まとめて登録では1回ごとに作り直す。
				AvatarVariation runVariation = avatarVariationData;
				GameObject basePrefab = prefab;
				string runShapeName = nailShapeName;
				GameObject ResolveRunPrefab()
				{
					if (runVariation.NailNodes == null || runVariation.NailNodes.Length == 0) return basePrefab;
					GameObject nodePrefab = NailPrefabBuilder.BuildTemporaryFromNodes(runVariation.NailNodes, runVariation.VariationName);
					GameObject resolvedNodePrefab = NailSetupProcessor.ResolveShapePrefab(nodePrefab, runShapeName, runVariation.NailNodes);
					if (!ReferenceEquals(resolvedNodePrefab, nodePrefab)) NailPrefabBuilder.DestroyTemporaryPrefab(nodePrefab);
					return resolvedNodePrefab;
				}

				// まとめて登録は途中で失敗したら先行分と既存メニューの初期値変更も含めて全部戻す。
				using NailSetupTransaction? batchTransaction = multiSelections == null ? null : new NailSetupTransaction(avatar.gameObject);

				var runs = new List<((INailProcessor, string, string)[] names, int value, bool isDefault, string? label, string shape)>();
				if (multiSelections == null)
				{
					runs.Add((this.GetNailProcessors(), 0, false, null, nailShapeName));
				}
				else
				{
					int usedMax = 0;
#if MD_NAIL_FOR_MA
					usedMax = NailSetupProcessor.PrepareExclusiveMenuGroup(avatar, MultiNailParameter);
#endif
					List<string> allShapes;
					using (DBNailShape dbNailShape = new()) allShapes = dbNailShape.collection.Select(s => s.ShapeName).ToList();
					var designCache = new Dictionary<string, INailProcessor>();
					for (int i = 0; i < multiSelections.Count; i++)
					{
						var sel = multiSelections[i];
						if (!designCache.TryGetValue(sel.designName, out INailProcessor? design))
						{
							design = INailProcessor.CreateNailDesign(sel.designName);
							designCache.Add(sel.designName, design);
						}
						// 形ごとにマテリアルがあるため、選んだ形に対応しないデザインは対応する最初の形で付ける。
						string shape = design.IsSupportedNailShape(nailShapeName)
							? nailShapeName
							: allShapes.FirstOrDefault(design.IsSupportedNailShape) ?? nailShapeName;
						runs.Add((BuildUniformNailSelection(design, sel.materialName, sel.colorName, isHandActive, isFootActive),
							usedMax + i + 1, sel.isStart, BuildMultiNailLabel(multiSelections, i), shape));
					}
				}

				var allWarnings = new List<string>();
				for (int runIndex = 0; runIndex < runs.Count; runIndex++)
				{
					var run = runs[runIndex];
					(INailProcessor, string, string)[] designAndVariationNames = run.names;
					runShapeName = run.shape;
					Mesh?[]? runMesh = run.shape == nailShapeName || !isHandActive ? overrideMesh : NailShapeDropDown.GetShapeMeshes(run.shape);
					NailSetupProcessor processor = new(avatar, avatarVariationData, ResolveRunPrefab(), designAndVariationNames, run.shape)
					{
						AvatarName = this._avatarDropDowns?.GetAvatarName(),
						OverrideMesh = runMesh,
						UseFootNail = isFootActive,
						// 2本目以降で外すと、同じ登録で入れた前のネイルまで消えてしまう。
						RemoveCurrentNail = runIndex == 0 && this._removeCurrentNail!.value,
						GenerateMaterial = directMaterial == null,  // 直接指定時は生成OFF
						Backup = this._backup!.value,
						ForModularAvatar = this._forModularAvatar!.value,
						OverrideMaterial = directMaterial,
						EnabledNailSlots = this._nailDesignDropDowns?.Select((dropdown, index) =>
							 dropdown.IsFingerEnabled && (index < 10 ? isHandActive : isFootActive)).ToArray(),
						GenerateExpressionMenu = (this._forModularAvatar?.value == true)
						                      && (this._generateExpressionMenu?.value == true),
						SplitHandFoot = (this._forModularAvatar?.value == true)
						             && (this._generateExpressionMenu?.value == true)
						             && (this._splitHandFootExpressionMenu?.value == true)
						             // まとめて登録はネイル1つ = メニュー1項目で切り替える。手足に分けると各ネイルがサブメニューになる。
						             && multiSelections == null,
						MergeAnLabo = (this._forModularAvatar?.value == true)
						           && (this._generateExpressionMenu?.value == true)
						           // まとめて登録は切替項目がトップメニューに散らばらないよう常に An-Labo の中へまとめる。
						           && (this._mergeAnLaboExpressionMenu?.value == true || multiSelections != null),
						ArmatureScaleCompensation = (this._armatureScaleCompensation?.value == true),
						BakeBlendShapes = (this._forModularAvatar?.value == true)
						               && (this._bakeBlendShapes?.value == true),
						SelectedBlendShapeVariantName = (!(this._forModularAvatar?.value == true && this._bakeBlendShapes?.value == true) && this._avatarDropDowns?.BlendShapeVariantPopup != null && this._avatarDropDowns.BlendShapeVariantPopup.index > 0) ? this._avatarDropDowns.BlendShapeVariantPopup.value : null,
						EnableAdditionalMaterials = true,
						// まとめて登録はデザインごとの既定の追加パーツを使う。画面の追加パーツ指定は単体選択中のデザイン向け。
						PerFingerAdditionalMaterials = multiSelections == null ? this.BuildPerFingerAdditionalMaterials(false) : null,
						AdditionalObjectsFactory = multiSelections == null ? new Func<IEnumerable<Transform>?[]?>(() => this.BuildPerFingerAdditionalObjects(false)) : null,
						ExclusiveMenuParameter = multiSelections == null ? null : MultiNailParameter,
						ExclusiveMenuValue = run.value,
						ExclusiveMenuIsDefault = run.isDefault,
						NailLabelOverride = run.label,
					};

					// AvatarEntityをprocessorにセット（shop.jsonのblendShapeVariantsを参照するため）
					processor.AvatarEntity = freshAvatarEntity;

					processor.Process();
					allWarnings.AddRange(processor.Warnings);
				}

				if (!isHandActive) this.RemoveHandNailObjects(avatar);
				if (!isFootActive) this.RemoveFootNailObjects(avatar);
				batchTransaction?.Complete();

				// 失敗して戻した分を着用回数に数えないよう、全件成功後に記録する。
				string avatarKey = this._avatarDropDowns!.GetAvatarKey();
				foreach (var run in runs) MDNailToolUsageStats.Update(run.names, avatarKey);

				this._nailDesignSelect!.Init();
				this.RefreshMultiNailMenuPreview();

				this.HideErrorBanner();
				if (allWarnings.Count > 0)
				{
					string warnSummary = string.Format(
						S("warning.variant_load_failed") ?? "一部のバリアントの読み込みに失敗しました ({0}件)",
						allWarnings.Count);
					this.ShowWarningBanner(warnSummary, allWarnings);
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

			DBShop.ClearCache();
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


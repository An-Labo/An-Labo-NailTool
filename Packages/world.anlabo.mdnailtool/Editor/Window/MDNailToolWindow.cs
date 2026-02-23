using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
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

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Window
{
	public class MDNailToolWindow : EditorWindow
	{
		public static void ShowWindow()
		{
			MDNailToolWindow window = CreateWindow<MDNailToolWindow>();
			window.titleContent = new GUIContent("An-Labo NailTool");
			window.Show();
		}

		#region Constants & Fields

		private const string SCENE_PREVIEW_NAME = "[MDNailTool_Preview]";

		private LocalizedObjectField? _materialObjectField;
		private LocalizedObjectField? _avatarObjectField;
		private AvatarDropDowns? _avatarDropDowns;
		private NailDesignSelect? _nailDesignSelect;
		private NailPreview? _nailPreview;
		private NailShapeDropDown? _nailShapeDropDown;
		private LocalizedDropDown? _nailMaterialDropDown;
		private LocalizedDropDown? _nailColorDropDown;

		private NailDesignDropDowns[]? _nailDesignDropDowns;

		private Toggle? _tglHandActive;
		private Toggle? _tglHandDetail;
		private Toggle? _tglFootActive;
		private Toggle? _tglFootDetail;

		private Toggle? _removeCurrentNail;
		private Toggle? _backup;
		private Toggle? _enableScenePreview;
		private Toggle? _forModularAvatar;
		private Toggle? _generateExpressionMenu;
		private Toggle? _splitHandFootExpressionMenu;
		private Toggle? _mergeAnLaboExpressionMenu;
		private Toggle? _bakeBlendShapes;
		private Toggle? _syncBlendShapesWithMA;
		private LocalizedButton? _execute;
		private LocalizedButton? _remove;

		private IVisualElementScheduledItem? _scenePreviewSchedule;
		private const int SCENE_PREVIEW_DEBOUNCE_MS = 150;

		private NailPreviewController? _nailPreviewController;

		private VisualElement? _handSelects;
		private VisualElement? _footSelects;

		private Label? _manualLink;
		private LocalizedLabel? _contactLink;

		// ---- Hand/Foot section headers (for error highlight) ----
		private VisualElement? _handSectionHeader;
		private VisualElement? _footSectionHeader;

		// ---- Error Banner ----
		private VisualElement? _errorBanner;
		private Label? _errorMessage;
		private Label? _errorDetailToggle;
		private VisualElement? _errorDetailArea;
		private Label? _errorDetailText;
		private bool _errorDetailExpanded = false;

		#endregion

		public void SetAvatar(Shop shop, Avatar? avatar, AvatarVariation? variation)
		{
			this._avatarDropDowns?.SetValues(shop, avatar, variation);
		}

		public void CreateGUI()
		{
			this.titleContent = new GUIContent("An-Labo NailTool");
			this.PrepareOnCreateGUI();
			this.BuildRootUI();
			this.BindCoreFields();
			this.BindAvatarUI();
			this.BindNailUI();
			this.BindHandFootUI();
			this.BindOptionsUI();
			this.BindLinksUI();
			this.BindErrorBanner();
			this.BindActions();
			this.PostInitSelection();
		}


		private void PrepareOnCreateGUI()
		{
			MDNailToolUsageStats.Migrate();
			INailProcessor.ClearPreviewMaterialCash();
			this.CleanupScenePreview();
		}
		private void BuildRootUI()
		{
			var uss = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(MDNailToolGuids.WindowUss);
			if (uss != null)
			{
				this.rootVisualElement.styleSheets.Add(uss);
			}

			var uxml = MDNailToolAssetLoader.LoadByGuid<VisualTreeAsset>(MDNailToolGuids.WindowUxml);
			if (uxml != null)
			{
				uxml.CloneTree(this.rootVisualElement);
			}
		}
		private void BindCoreFields()
		{
			this._materialObjectField = this.rootVisualElement.Q<LocalizedObjectField>("material-object");
			this._materialObjectField.RegisterValueChangedCallback(this.OnChangeMaterial);

			this._avatarObjectField = this.rootVisualElement.Q<LocalizedObjectField>("avatar-object");
			this._avatarObjectField.RegisterValueChangedCallback(this.OnChangeAvatar);
		}
		private void BindAvatarUI()
		{
			this._avatarDropDowns = this.rootVisualElement.Q<AvatarDropDowns>("avatar");
			if (this._avatarDropDowns == null) return;

			// tooltip.avatar_dropdowns はUxmlTraitsが非対応のためC#で設定
			this._avatarDropDowns.tooltip = S("tooltip.avatar_dropdowns");

			this._avatarDropDowns.SearchButtonClicked += this.ShowAvatarSearchWindow;
			this._avatarDropDowns.SortOrderSelected += this.OnChangeAvatarSortOrder;

			this._avatarDropDowns.RegisterCallback<ChangeEvent<string>>(evt =>
			{
				this.CleanupScenePreview();
				this.UpdatePreview();
				this.RequestScenePreviewUpdate();
				
				if (evt.target != this._avatarDropDowns.BlendShapeVariantPopup)
				{
					this.UpdateBlendShapeVariantDropDown();
				}
			});
		}

		private void BindNailUI()
		{
			this._nailDesignSelect = this.rootVisualElement.Q<NailDesignSelect>("nail-select");
			this._nailDesignSelect.OnSelectNail += this.OnSelectNail;
			this._nailDesignSelect.OnSearchButtonClicked += this.ShowNailSearchWindow;

			this._nailPreview = this.rootVisualElement.Q<NailPreview>("nail-preview");
			this._nailPreviewController = new NailPreviewController(this._nailPreview);

			this._nailShapeDropDown = this.rootVisualElement.Q<NailShapeDropDown>("nail-shape");
			this._nailShapeDropDown.SetNailShape(GlobalSetting.LastUseShapeName);
			this._nailShapeDropDown.RegisterValueChangedCallback(this.OnChangeShapeDropDown);

			this._nailMaterialDropDown = this.rootVisualElement.Q<LocalizedDropDown>("nail-material");
			this._nailMaterialDropDown.RegisterValueChangedCallback(this.OnChangeNailMaterialDropDown);

			this._nailColorDropDown = this.rootVisualElement.Q<LocalizedDropDown>("nail-color");
			this._nailColorDropDown.RegisterValueChangedCallback(this.OnChangeNailColorDropDown);
		}

		private void BindHandFootUI()
		{
			this.SetupFootVisualElements();
			this.InitializeNailDesignDropDowns();
			this.InitializeHandFootControl();
		}
		private void BindOptionsUI()
		{
			this._removeCurrentNail = this.rootVisualElement.Q<Toggle>("remove-current-nail");
			this._removeCurrentNail.SetValueWithoutNotify(GlobalSetting.RemoveCurrentNail);
			this._removeCurrentNail.RegisterValueChangedCallback(OnChangeRemoveCurrentNail);
			// ラベルクリックでトグル
			var lblRemove = this.rootVisualElement.Q<LocalizedLabel>("label-remove-nail");
			lblRemove?.RegisterCallback<ClickEvent>(_ => { if (this._removeCurrentNail != null) this._removeCurrentNail.value = !this._removeCurrentNail.value; });

			this._backup = this.rootVisualElement.Q<Toggle>("backup");
			this._backup.SetValueWithoutNotify(GlobalSetting.Backup);
			this._backup.RegisterValueChangedCallback(OnChangeBackup);
			var lblBackup = this.rootVisualElement.Q<LocalizedLabel>("label-backup");
			lblBackup?.RegisterCallback<ClickEvent>(_ => { if (this._backup != null) this._backup.value = !this._backup.value; });

			// プレビューウィンドウ表示ON/OFFトグル（プレビューヘッダー内）
			this._enableScenePreview = this.rootVisualElement.Q<Toggle>("enable-scene-preview");
			if (this._enableScenePreview != null)
			{
				this._enableScenePreview.SetValueWithoutNotify(GlobalSetting.EnableScenePreview);
				this._enableScenePreview.RegisterValueChangedCallback(this.OnChangePreviewWindowVisible);
				this.UpdatePreviewAreaVisibility(GlobalSetting.EnableScenePreview);
			}
			var lblPreview = this.rootVisualElement.Q<LocalizedLabel>("label-preview-toggle");
			lblPreview?.RegisterCallback<ClickEvent>(_ => { if (this._enableScenePreview != null) this._enableScenePreview.value = !this._enableScenePreview.value; });

			// 着用プレビュー（Sceneプレビュー）トグル（詳細設定内）
			var tglWearingPreview = this.rootVisualElement.Q<Toggle>("enable-wearing-preview");
			if (tglWearingPreview != null)
			{
				tglWearingPreview.SetValueWithoutNotify(GlobalSetting.EnableSceneWearingPreview);
				tglWearingPreview.RegisterValueChangedCallback(this.OnChangeEnableScenePreview);
			}
			var lblWearingPreview = this.rootVisualElement.Q<LocalizedLabel>("label-wearing-preview");
			lblWearingPreview?.RegisterCallback<ClickEvent>(_ => { if (tglWearingPreview != null) tglWearingPreview.value = !tglWearingPreview.value; });

			this._forModularAvatar = this.rootVisualElement.Q<Toggle>("for-modular-avatar");
			if (this._forModularAvatar != null)
			{
				this._forModularAvatar.SetValueWithoutNotify(GlobalSetting.UseModularAvatar);
				this._forModularAvatar.RegisterValueChangedCallback(this.OnChangeForModularAvatar);
				var lblMA = this.rootVisualElement.Q<LocalizedLabel>("label-modular-avatar");
				lblMA?.RegisterCallback<ClickEvent>(_ => {
					if (this._forModularAvatar != null) this._forModularAvatar.value = !this._forModularAvatar.value;
				});
			}

			this._generateExpressionMenu = this.rootVisualElement.Q<Toggle>("generate-expression-menu");
			if (this._generateExpressionMenu != null)
			{
				this._generateExpressionMenu.SetValueWithoutNotify(GlobalSetting.GenerateExpressionMenu);
				this._generateExpressionMenu.RegisterValueChangedCallback(evt => {
					GlobalSetting.GenerateExpressionMenu = evt.newValue;
					this.UpdateExpressionMenuSubOptions(evt.newValue);
				});
				this._generateExpressionMenu.SetEnabled(GlobalSetting.UseModularAvatar);
				var lblGenMenu = this.rootVisualElement.Q<LocalizedLabel>("label-generate-expression-menu");
				lblGenMenu?.RegisterCallback<ClickEvent>(_ => {
					if (this._generateExpressionMenu != null && this._generateExpressionMenu.enabledSelf)
						this._generateExpressionMenu.value = !this._generateExpressionMenu.value;
				});
			}

			this._splitHandFootExpressionMenu = this.rootVisualElement.Q<Toggle>("split-hand-foot-expression-menu");
			if (this._splitHandFootExpressionMenu != null)
			{
				this._splitHandFootExpressionMenu.SetValueWithoutNotify(GlobalSetting.SplitHandFootExpressionMenu);
				this._splitHandFootExpressionMenu.RegisterValueChangedCallback(
					evt => GlobalSetting.SplitHandFootExpressionMenu = evt.newValue);
				this._splitHandFootExpressionMenu.SetEnabled(GlobalSetting.UseModularAvatar && GlobalSetting.GenerateExpressionMenu);
				var lbl = this.rootVisualElement.Q<LocalizedLabel>("label-split-hand-foot");
				lbl?.RegisterCallback<ClickEvent>(_ => {
					if (this._splitHandFootExpressionMenu != null && this._splitHandFootExpressionMenu.enabledSelf)
						this._splitHandFootExpressionMenu.value = !this._splitHandFootExpressionMenu.value;
				});
			}

			this._mergeAnLaboExpressionMenu = this.rootVisualElement.Q<Toggle>("merge-anlabo-expression-menu");
			if (this._mergeAnLaboExpressionMenu != null)
			{
				this._mergeAnLaboExpressionMenu.SetValueWithoutNotify(GlobalSetting.MergeAnLaboExpressionMenu);
				this._mergeAnLaboExpressionMenu.RegisterValueChangedCallback(
					evt => GlobalSetting.MergeAnLaboExpressionMenu = evt.newValue);
				this._mergeAnLaboExpressionMenu.SetEnabled(GlobalSetting.UseModularAvatar && GlobalSetting.GenerateExpressionMenu);
				var lbl = this.rootVisualElement.Q<LocalizedLabel>("label-merge-anlabo");
				lbl?.RegisterCallback<ClickEvent>(_ => {
					if (this._mergeAnLaboExpressionMenu != null && this._mergeAnLaboExpressionMenu.enabledSelf)
						this._mergeAnLaboExpressionMenu.value = !this._mergeAnLaboExpressionMenu.value;
				});
			}

			this._bakeBlendShapes = this.rootVisualElement.Q<Toggle>("bake-blendshapes");
			if (this._bakeBlendShapes != null)
			{
				this._bakeBlendShapes.SetValueWithoutNotify(GlobalSetting.BakeBlendShapes);
				this._bakeBlendShapes.RegisterValueChangedCallback(evt => {
					GlobalSetting.BakeBlendShapes = evt.newValue;
					this._syncBlendShapesWithMA?.SetEnabled(evt.newValue);
					this.UpdateBlendShapeVariantVisibility();
				});
				this._bakeBlendShapes.SetEnabled(GlobalSetting.UseModularAvatar);
				var lblBake = this.rootVisualElement.Q<LocalizedLabel>("label-bake-blendshapes");
				lblBake?.RegisterCallback<ClickEvent>(_ => {
					if (this._bakeBlendShapes != null && this._bakeBlendShapes.enabledSelf)
						this._bakeBlendShapes.value = !this._bakeBlendShapes.value;
				});
			}

			this._syncBlendShapesWithMA = this.rootVisualElement.Q<Toggle>("sync-blendshapes-with-ma");
			if (this._syncBlendShapesWithMA != null)
			{
				this._syncBlendShapesWithMA.SetValueWithoutNotify(GlobalSetting.SyncBlendShapesWithMA);
				this._syncBlendShapesWithMA.RegisterValueChangedCallback(
					evt => GlobalSetting.SyncBlendShapesWithMA = evt.newValue);
				this._syncBlendShapesWithMA.SetEnabled(GlobalSetting.UseModularAvatar && GlobalSetting.BakeBlendShapes);
				var lblSync = this.rootVisualElement.Q<LocalizedLabel>("label-sync-blendshapes");
				lblSync?.RegisterCallback<ClickEvent>(_ => {
					if (this._syncBlendShapesWithMA != null && this._syncBlendShapesWithMA.enabledSelf)
						this._syncBlendShapesWithMA.value = !this._syncBlendShapesWithMA.value;
				});
			}
		}

		private void UpdateExpressionMenuSubOptions(bool exprMenuEnabled)
		{
			this._splitHandFootExpressionMenu?.SetEnabled(exprMenuEnabled);
			this._mergeAnLaboExpressionMenu?.SetEnabled(exprMenuEnabled);
		}

		private void UpdateBlendShapeVariantDropDown()
		{
			if (this._avatarDropDowns == null) return;
			var popup = this._avatarDropDowns.BlendShapeVariantPopup;
			if (popup == null) return;

			var choices = new List<string> { S("window.none") ?? "None" };
			popup.choices = choices;
			popup.index = 0;

			var avatarVariationData = this._avatarDropDowns.GetSelectedAvatarVariation();
			if (avatarVariationData == null) 
			{
				this.UpdateBlendShapeVariantVisibility();
				return;
			}

			AvatarBlendShapeVariant[]? variants = avatarVariationData.BlendShapeVariants;
			if (variants == null)
			{
				using DBShop dbShop = new();
				string avatarName = this._avatarDropDowns.GetAvatarName();
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

			if (variants != null && variants.Length > 0)
			{
				choices.AddRange(variants.Select(v => v.Name));
				popup.choices = choices;
			}
			
			this.UpdateBlendShapeVariantVisibility();
		}

		private void UpdateBlendShapeVariantVisibility()
		{
			if (this._avatarDropDowns == null) return;
			var popup = this._avatarDropDowns.BlendShapeVariantPopup;
			if (popup == null) return;

			bool maEnabled = GlobalSetting.UseModularAvatar;
			bool bakeEnabled = GlobalSetting.BakeBlendShapes;
			
			bool hasVariants = popup.choices.Count > 1;
			
			popup.style.display = DisplayStyle.Flex;
			popup.SetEnabled(maEnabled && !bakeEnabled && hasVariants);
		}

		private void UpdatePreviewAreaVisibility(bool visible)
		{
			var area = this.rootVisualElement.Q<VisualElement>("nail-preview-area");
			if (area != null)
				area.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}
		private void BindLinksUI()
		{
			this._manualLink = this.rootVisualElement.Q<Label>("link-manual");
			this._manualLink?.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.manual")));

			// ヘッダーのFAQリンク
			var headerContact = this.rootVisualElement.Q<Label>("link-contact-header");
			headerContact?.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.contact")));

			// フッターのコンタクトリンク
			this._contactLink = this.rootVisualElement.Q<LocalizedLabel>("link-contact");
			this._contactLink?.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.contact")));

			// ヘッダーのバージョン表記
		var versionStr = MDNailToolDefines.Version;
		var headerVersion = this.rootVisualElement.Q<Label>("version");
		if (headerVersion != null)
			headerVersion.text = "v" + versionStr;

		// フッターのバージョン表記
		var footerVersion = this.rootVisualElement.Q<Label>("version-footer");
		if (footerVersion != null)
			footerVersion.text = versionStr;
		}

		private void BindErrorBanner()
		{
			this._errorBanner = this.rootVisualElement.Q<VisualElement>("error-banner");
			this._errorMessage = this.rootVisualElement.Q<Label>("error-message");
			this._errorDetailToggle = this.rootVisualElement.Q<Label>("error-detail-toggle");
			this._errorDetailArea = this.rootVisualElement.Q<VisualElement>("error-detail-area");
			this._errorDetailText = this.rootVisualElement.Q<Label>("error-detail-text");

			this.rootVisualElement.Q<Button>("error-close")
				?.RegisterCallback<ClickEvent>(_ => {
					this.HideErrorBanner();
					this.ClearHandFootError();
					this.ClearAvatarFieldError();
				});

			this._errorDetailToggle?.RegisterCallback<ClickEvent>(_ => this.ToggleErrorDetail());

			var copyBtn = this.rootVisualElement.Q<Button>("error-copy");
			if (copyBtn != null)
			{
				copyBtn.text = S("error.copy") ?? "Copy to Clipboard";
				copyBtn.RegisterCallback<ClickEvent>(_ =>
					GUIUtility.systemCopyBuffer = this._errorDetailText?.text ?? "");
			}

			var contactBtn = this.rootVisualElement.Q<Button>("error-contact");
			if (contactBtn != null)
			{
				contactBtn.text = S("window.contact") ?? "Contact";
				contactBtn.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.contact")));
			}
		}

		private void ShowErrorBanner(string? userMessage, Exception? ex = null)
		{
			if (this._errorBanner == null) return;
			this._errorBanner.style.display = DisplayStyle.Flex;
			if (this._errorMessage != null) this._errorMessage.text = userMessage ?? "";
			if (this._errorDetailText != null) this._errorDetailText.text = ex?.ToString() ?? "";
			this._errorDetailExpanded = false;
			if (this._errorDetailArea != null) this._errorDetailArea.style.display = DisplayStyle.None;
			if (this._errorDetailToggle != null)
				this._errorDetailToggle.text = S("error.show_detail") ?? "▶ Show Details";
			// Only show the detail toggle when there's exception detail to show
			if (this._errorDetailToggle != null)
				this._errorDetailToggle.style.display = ex != null ? DisplayStyle.Flex : DisplayStyle.None;
			// ScrollView内でエラーバナーが見えるようにスクロール
			var scrollView = this.rootVisualElement.Q<ScrollView>("Root");
			if (scrollView != null && this._errorBanner != null)
				this._errorBanner.schedule.Execute(() => scrollView.ScrollTo(this._errorBanner));
		}

		private void HideErrorBanner()
		{
			if (this._errorBanner != null) this._errorBanner.style.display = DisplayStyle.None;
		}

		private void ToggleErrorDetail()
		{
			this._errorDetailExpanded = !this._errorDetailExpanded;
			if (this._errorDetailArea != null)
				this._errorDetailArea.style.display = this._errorDetailExpanded ? DisplayStyle.Flex : DisplayStyle.None;
			if (this._errorDetailToggle != null)
				this._errorDetailToggle.text = this._errorDetailExpanded
					? (S("error.hide_detail") ?? "▼ Hide Details")
					: (S("error.show_detail") ?? "▶ Show Details");
		}

		private void ShowAvatarFieldError()
		{
			this._avatarObjectField?.AddToClassList("mdn-field-error");
		}

		private void ClearAvatarFieldError()
		{
			this._avatarObjectField?.RemoveFromClassList("mdn-field-error");
		}

		private void ShowHandFootError()
		{
			this._handSectionHeader?.AddToClassList("mdn-field-error");
			this._footSectionHeader?.AddToClassList("mdn-field-error");
		}

		private void ClearHandFootError()
		{
			this._handSectionHeader?.RemoveFromClassList("mdn-field-error");
			this._footSectionHeader?.RemoveFromClassList("mdn-field-error");
		}

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

			footSelects.Add(leftHeader);

			// ---- 左足 5本 ----
			string[] leftToes = { "left-foot-thumb", "left-foot-index", "left-foot-middle", "left-foot-ring", "left-foot-little" };
			for (int i = 0; i < 5; i++)
			{
				var dd = new NailDesignDropDowns { name = leftToes[i] };
				footSelects.Add(dd);

				var innerDropdown = dd.Q<DropdownField>("NailDesignDropDowns-DesignDropDown");
				if (innerDropdown is DropdownField ddf)
				{
					ddf.label = S(toeTextIds[i]) ?? toeTextIds[i];
				}
			}

			// ---- 右足区切り行 ----
			var divider = new VisualElement();
			divider.AddToClassList("mdn-hand-divider");

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
				footSelects.Add(dd);

				var innerDropdown = dd.Q<DropdownField>("NailDesignDropDowns-DesignDropDown");
				if (innerDropdown is DropdownField ddf)
				{
					ddf.label = S(toeTextIds[i]) ?? toeTextIds[i];
				}
			}
		}

		private void InitializeNailDesignDropDowns()
		{
			this._nailDesignDropDowns = new[] {
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

			this._nailDesignDropDowns = this._nailDesignDropDowns.Where(d => d != null).ToArray();

			foreach (NailDesignDropDowns nailDesignDropDown in this._nailDesignDropDowns)
			{
				nailDesignDropDown.RegisterCallback<ChangeEvent<string?>>(this.OnChangeNailDesign);
			}
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
				if (result != null) this._avatarDropDowns!.SetValues(result.Value.shop, result.Value.avatar, result.Value.variation);

				this.CleanupScenePreview();
				this.UpdatePreview();
				this.RequestScenePreviewUpdate();
			}
		}

		private void OnDestroy()
		{
			INailProcessor.ClearPreviewMaterialCash();
			this.CleanupScenePreview();
		}

		private world.anlabo.mdnailtool.Editor.Window.Controllers.MDNailScenePreviewController? _scenePreviewController;


		private void OnExecute()
		{
			this.CleanupScenePreview();
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
			AssetDatabase.StartAssetEditing();
			try
			{
				(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();

				Mesh?[]? selectedMeshes = this._nailShapeDropDown!.GetSelectedShapeMeshes();
				Mesh?[]? overrideMesh = isHandActive ? selectedMeshes : new Mesh?[10];

				Material? directMaterial = this._materialObjectField!.value as Material;

				NailSetupProcessor processor = new(avatar, avatarVariationData, prefab, designAndVariationNames, nailShapeName)
				{
					AvatarName = this._avatarDropDowns.GetAvatarName(),
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
					BakeBlendShapes = (this._forModularAvatar?.value == true)
					               && (this._bakeBlendShapes?.value == true),
					SyncBlendShapesWithMA = (this._forModularAvatar?.value == true)
					                     && (this._bakeBlendShapes?.value == true)
					                     && (this._syncBlendShapesWithMA?.value == true),
					SelectedBlendShapeVariantName = (this._forModularAvatar?.value == true && this._bakeBlendShapes?.value == false && this._avatarDropDowns?.BlendShapeVariantPopup != null && this._avatarDropDowns.BlendShapeVariantPopup.index > 0) ? this._avatarDropDowns.BlendShapeVariantPopup.value : null,
				};

				// AvatarEntityをprocessorにセット（shop.jsonのblendShapeVariantsを参照するため）
				{
					using DBShop dbShop = new();
					string avatarName = this._avatarDropDowns.GetAvatarName();
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
				string successMessage = BuildSuccessMessage(isHandActive, isFootActive);
				EditorUtility.DisplayDialog(S("dialog.finished"), successMessage, "OK");
			}
			catch (Exception e)
			{
				Debug.LogError(e);
				this.ShowErrorBanner(S("error.execute.failed"), e);
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
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

		private void OnSelectNail(string designName)
		{
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

			this.UpdateNailShapeFilter(nailProcessor);
			this.UpdatePreview();
			this.RequestScenePreviewUpdate();
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
			if (this._materialObjectField!.value != null) { this._nailShapeDropDown!.SetFilter(_ => true); return; }
			if (processor != null) { this._nailShapeDropDown!.SetFilter(processor.IsSupportedNailShape); return; }

			HashSet<string> designNameSet = this._nailDesignDropDowns!.Select(downs => downs.GetSelectedDesignName()).ToHashSet();
			using DBNailDesign dbNailDesign = new();
			List<INailProcessor> processors = designNameSet.Select(INailProcessor.CreateNailDesign).ToList();
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
			Material? directMaterial = this._materialObjectField?.value as Material;

			this._nailPreviewController?.ChangeNailMaterial(designAndVariationNames, nailShapeName, directMaterial);
			this._nailPreviewController?.ChangeAdditionalObjects(designAndVariationNames, nailShapeName);
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

			Mesh?[]? overrideMeshes = this._nailShapeDropDown?.GetSelectedShapeMeshes();
			if (overrideMeshes == null) overrideMeshes = new Mesh?[0];

			bool isHandActive = this._tglHandActive?.value ?? true;
			bool isFootActive = this._tglFootActive?.value ?? false;

			(INailProcessor, string, string)[] designAndVariationNames = this.GetNailProcessors();
			Material? directMaterial = this._materialObjectField?.value as Material;

			this._scenePreviewController ??= new MDNailScenePreviewController(SCENE_PREVIEW_NAME);
			this._scenePreviewController.Update(
				avatar,
				prefab,
				overrideMeshes,
				nailShapeName,
				isHandActive,
				isFootActive,
				designAndVariationNames,
				directMaterial
			);
		}


		private void CleanupScenePreview()
		{
			this._scenePreviewController?.Cleanup(this._avatarObjectField?.value as VRCAvatarDescriptor);
			this._scenePreviewController = null;
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

		private static void OnChangeRemoveCurrentNail(ChangeEvent<bool> evt) { GlobalSetting.RemoveCurrentNail = evt.newValue; }
		private static void OnChangeBackup(ChangeEvent<bool> evt) { GlobalSetting.Backup = evt.newValue; }
		private void OnChangeForModularAvatar(ChangeEvent<bool> evt)
		{
			GlobalSetting.UseModularAvatar = evt.newValue;
			if (this._generateExpressionMenu != null)
			{
				this._generateExpressionMenu.SetEnabled(evt.newValue);
				this._splitHandFootExpressionMenu?.SetEnabled(evt.newValue && this._generateExpressionMenu.value);
				this._mergeAnLaboExpressionMenu?.SetEnabled(evt.newValue && this._generateExpressionMenu.value);
			}
			this._bakeBlendShapes?.SetEnabled(evt.newValue);
			this._syncBlendShapesWithMA?.SetEnabled(evt.newValue && (this._bakeBlendShapes?.value == true));
			this.UpdateBlendShapeVariantVisibility();
		}
		private void ShowAvatarSearchWindow() { SearchAvatarWindow.ShowWindow(this); }
		private void ShowNailSearchWindow() { SearchNailDesignWindow.ShowWindow(this); }
		public void SelectNailFromSearch(string designName) { this.OnSelectNail(designName); }

		// プレビューウィンドウ（NailPreview）の表示/非表示
		private void OnChangePreviewWindowVisible(ChangeEvent<bool> evt)
		{
			GlobalSetting.EnableScenePreview = evt.newValue;
			this.UpdatePreviewAreaVisibility(evt.newValue);
		}

		// 着用プレビュー（Sceneへの仮着用）のON/OFF
		private void OnChangeEnableScenePreview(ChangeEvent<bool> evt)
		{
			GlobalSetting.EnableSceneWearingPreview = evt.newValue;

			var avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			if (avatar != null)
			{
				this._scenePreviewController ??= new MDNailScenePreviewController(SCENE_PREVIEW_NAME);
				this._scenePreviewController.SetScenePreviewActive(avatar, evt.newValue);
			}

			if (evt.newValue) this.UpdateScenePreview(immediate: true);
		}

	}
}
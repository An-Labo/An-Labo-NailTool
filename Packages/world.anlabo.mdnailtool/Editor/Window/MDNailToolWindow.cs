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

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Window
{
	public partial class MDNailToolWindow : EditorWindow
	{
		public static void ShowWindow()
		{
			MDNailToolWindow window = CreateWindow<MDNailToolWindow>();
			window.titleContent = new GUIContent("An-Labo NailTool");
			window.Show();
		}

		#region Constants & Fields

		private const string SCENE_PREVIEW_NAME = "[MDNailTool_Preview]";

		private Toggle? _enableDirectMaterial;
		private ObjectField? _materialObjectField;
		private LocalizedObjectField? _avatarObjectField;
		private AvatarDropDowns? _avatarDropDowns;
		private NailDesignSelect? _nailDesignSelect;
		private NailPreview? _nailPreview;
		private NailShapeDropDown? _nailShapeDropDown;
		private LocalizedDropDown? _nailMaterialDropDown;
		private LocalizedDropDown? _nailColorDropDown;
		private DropdownField? _nailVariantDropDown;
		private Dictionary<string, string>? _variantDisplayNames;

		private NailDesignDropDowns[]? _nailDesignDropDowns;

		private Toggle? _tglHandActive;
		private Toggle? _tglHandDetail;
		private Toggle? _tglFootActive;
		private Toggle? _tglFootDetail;

		private Toggle? _bulkLeftHand;
		private Toggle? _bulkRightHand;
		private Toggle? _bulkLeftFoot;
		private Toggle? _bulkRightFoot;

		private Toggle? _removeCurrentNail;
		private Toggle? _backup;
		private Toggle? _enableScenePreview;
		private Toggle? _forModularAvatar;
		private Toggle? _generateExpressionMenu;
		private Toggle? _splitHandFootExpressionMenu;
		private Toggle? _mergeAnLaboExpressionMenu;
		private Toggle? _armatureScaleCompensation;
		private Toggle? _penetrationCorrection;
		private Toggle? _bakeBlendShapes;
		private Toggle? _syncBlendShapesWithMA;
		private Toggle? _autoLinkShrinkBS;
		private Toggle? _closeWindowOnExecute;
		private DropdownField? _additionalMaterialSourceDropdown;
		private DropdownField? _additionalObjectSourceDropdown;
		private LocalizedButton? _execute;
		private LocalizedButton? _remove;
		private Button? _tryoutToggle;
		private VisualElement? _tryoutBanner;
		private bool _tryoutActive;

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
		private int _userErrorCount = 0;
		private VisualElement? _contactLinksArea;

		// ---- Warning Banner ----
		private VisualElement? _warningBanner;
		private Label? _warningMessage;
		private Label? _warningDetailToggle;
		private VisualElement? _warningDetailArea;
		private Label? _warningDetailText;
		private bool _warningDetailExpanded = false;

		// ---- Tool Console ----
		private VisualElement? _toolConsoleContainer;
		private ScrollView? _toolConsoleScroll;

		// ---- Shader Preset ----
		private DropdownField? _shaderPresetSelect;
		private Button? _shaderPresetReloadBtn;
		private Button? _shaderPresetPingBtn;
		private ObjectField? _shaderPresetAddField;
		private Button? _shaderPresetSaveBtn;
		private Button? _shaderPresetSettingsToggleBtn;
		private VisualElement? _shaderPresetSettingsArea;
		private VisualElement? _shaderPresetSettingsList;
		private bool _shaderPresetSettingsOpen;
		private const string SHADER_PRESET_NONE_LABEL = "Nail Default";

		#endregion

		public void SetAvatar(Shop shop, Avatar? avatar, AvatarVariation? variation)
		{
			this._avatarDropDowns?.SetValues(shop, avatar, variation);
			this.UpdateBlendShapeVariantDropDown();
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
			this.BindWarningBanner();
			this.BindActions();
			this.CheckNailChipV2Update();
			this.PostInitSelection();
		}

		private enum ChipVersion { NotInstalled, V1, V2 }

		// CreateGUI毎にfbx走査は重いのでプロセス中キャッシュ。ProjectChangedで無効化。
		private static ChipVersion? _cachedAnLaboVersion;
		private static ChipVersion? _cachedMDollVersion;
		private static bool _chipCacheInvalidationSubscribed;

		private static void InvalidateChipVersionCache()
		{
			_cachedAnLaboVersion = null;
			_cachedMDollVersion = null;
		}

		private static void EnsureChipCacheInvalidationHook()
		{
			if (_chipCacheInvalidationSubscribed) return;
			_chipCacheInvalidationSubscribed = true;
			EditorApplication.projectChanged += InvalidateChipVersionCache;
		}

		// バッジクリック動作。CreateGUI時とDebug呼び出し時で差し替えるためフィールドに保持。
		// ラムダ式でRegisterCallbackすると都度別インスタンスになりUnregister不可のため、
		// 固定ハンドラ経由でActionを差し替える方式にする。
		private Action? _badgeClickAction;
		private EventCallback<ClickEvent>? _badgeClickCallback;

		private void BindBadgeClickHandler(LocalizedLabel label, Action? onClick)
		{
			this._badgeClickAction = onClick;
			if (this._badgeClickCallback == null)
			{
				this._badgeClickCallback = _ => this._badgeClickAction?.Invoke();
				label.RegisterCallback(this._badgeClickCallback);
			}
			if (onClick != null) label.AddToClassList("mdn-option-clickable");
		}

		private void CheckNailChipV2Update()
		{
			EnsureChipCacheInvalidationHook();
			// slot [0]=An-Labo v2 / [1]=MDollnail / [2]=An-Labo v1
			ChipVersion anLabo = _cachedAnLaboVersion ??= DetectChipVersionAtSlot(0, fallbackSlot: 2);
			ChipVersion mdoll = _cachedMDollVersion ??= DetectChipVersionAtSlot(1);

			// 片方でもv2あれば何も表示しない
			if (anLabo == ChipVersion.V2 || mdoll == ChipVersion.V2) return;

			bool anLaboV1 = anLabo == ChipVersion.V1;
			bool mdollV1 = mdoll == ChipVersion.V1;

			var warningLabel = this.rootVisualElement.Q<LocalizedLabel>("nail-chip-update-warning");
			if (warningLabel == null) return;

			Action? onClick = null;

			if (mdollV1 && anLaboV1)
			{
				// ⑤両方v1 → MDoll優先
				onClick = OpenMDollBoothUrl;
			}
			else if (mdollV1)
			{
				// ⑧MDoll v1のみ
				onClick = OpenMDollBoothUrl;
			}
			else if (anLaboV1)
			{
				// ⑥An-Labo v1のみ
				onClick = OpenAnLaboUpdateUrl;
			}
			else
			{
				// ⑨両方なし (未購入) → ダイアログで選択
				onClick = ShowPurchaseSelectionDialog;
			}

			warningLabel.style.display = DisplayStyle.Flex;
			this.BindBadgeClickHandler(warningLabel, onClick);
		}

		private static void OpenMDollBoothUrl()
		{
			if (!string.IsNullOrEmpty(MDNailToolDefines.MDOLL_NAIL_BOOTH_URL))
				Application.OpenURL(MDNailToolDefines.MDOLL_NAIL_BOOTH_URL);
		}

		/// <summary>An-Labo v1→v2 更新誘導 (⑥): 所有デザインの中から一番使ってる/新しいURLを開く</summary>
		private static void OpenAnLaboUpdateUrl()
		{
			string? url = FindMostUsedDesignUrl();
			if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
		}

		/// <summary>An-Labo 未購入案内 (⑨): ネイルラボ (コレクションサイト) を開く</summary>
		private static void OpenAnLaboNailLabUrl()
		{
			if (!string.IsNullOrEmpty(MDNailToolDefines.ANLABO_NAILLAB_URL))
				Application.OpenURL(MDNailToolDefines.ANLABO_NAILLAB_URL);
		}

		private static void ShowPurchaseSelectionDialog()
		{
			// ⑨未購入 → MD / An-Labo / キャンセル の順で3択
			int choice = EditorUtility.DisplayDialogComplex(
				S("window.nail_chip_purchase_title"),
				S("window.nail_chip_purchase_message"),
				S("window.nail_chip_purchase_mdoll"),
				S("window.nail_chip_purchase_cancel"),
				S("window.nail_chip_purchase_anlabo"));

			switch (choice)
			{
				case 0: OpenMDollBoothUrl(); break;
				case 2: OpenAnLaboNailLabUrl(); break;
			}
		}

		private static string? FindMostUsedDesignUrl()
		{
			Dictionary<string, int> useCount = GlobalSetting.DesignUseCount;
			using DBNailDesign db = new();

			// 使用履歴 (カウント1以上) で多い順にURL持ちを探す (所有チェック付き)
			foreach (var entry in useCount.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value))
			{
				NailDesign? design = db.FindNailDesignByDesignName(entry.Key);
				if (IsOwnedDesignWithUrl(design)) return design!.Url;
			}

			// 履歴なし/履歴中に所有デザインなし → 所有デザインの中で Id降順 (新しい順) でURL持ちを探す
			foreach (NailDesign design in db.collection.OrderByDescending(d => d.Id))
			{
				if (IsOwnedDesignWithUrl(design)) return design.Url;
			}
			return null;
		}

		// サムネGUIDがプロジェクト内で解決できる = そのデザイン資産をimport済み = 所有とみなす。
		private static bool IsOwnedDesignWithUrl(NailDesign? design)
		{
			if (design == null || string.IsNullOrEmpty(design.Url)) return false;
			if (string.IsNullOrEmpty(design.ThumbnailGUID)) return false;
			string path = AssetDatabase.GUIDToAssetPath(design.ThumbnailGUID);
			return !string.IsNullOrEmpty(path);
		}

		/// <summary>nailShape.json の全shapeを走査し、指定スロットのフォルダからblendshape判定でv1/v2を検出。
		/// 指定スロットが解決できない場合は fallbackSlot を使用 (An-Labo の v2枠->v1枠フォールバック用)</summary>
		private static ChipVersion DetectChipVersionAtSlot(int slot, int fallbackSlot = -1)
		{
			// shape個別のv1検出で打ち切ると、後続shapeにv2が入っていても見逃すので全shape走査。
			// v2はどこか1つでも見つかれば即確定、v1は全shape確認後に決定する。
			using DBNailShape db = new();
			bool anyFolderFound = false;
			bool anyV1 = false;
			foreach (NailShape shape in db.collection)
			{
				ChipVersion v = DetectFromShapeSlot(shape.FbxFolderGUID, slot, fallbackSlot, ref anyFolderFound);
				if (v == ChipVersion.V2) return ChipVersion.V2;
				ChipVersion vf = DetectFromShapeSlot(shape.FootFbxFolderGUID, slot, fallbackSlot, ref anyFolderFound);
				if (vf == ChipVersion.V2) return ChipVersion.V2;
				if (v == ChipVersion.V1 || vf == ChipVersion.V1) anyV1 = true;
			}
			if (anyV1) return ChipVersion.V1;
			return anyFolderFound ? ChipVersion.V1 : ChipVersion.NotInstalled;
		}

		private static ChipVersion DetectFromShapeSlot(string[] guids, int slot, int fallbackSlot, ref bool anyFolderFound)
		{
			string? folder = ResolveSlotFolder(guids, slot);
			if (folder == null && fallbackSlot >= 0)
			{
				folder = ResolveSlotFolder(guids, fallbackSlot);
			}
			if (folder == null) return ChipVersion.NotInstalled;
			anyFolderFound = true;
			return DetectVersionFromFolder(folder);
		}

		private static string? ResolveSlotFolder(string[] guids, int index)
		{
			if (guids == null || index < 0 || index >= guids.Length) return null;
			string guid = guids[index];
			if (string.IsNullOrEmpty(guid)) return null;
			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path)) return null;
			return path;
		}

		/// <summary>デバッグ用: バッジを強制表示。クリック動作を選択してテストできる</summary>
		internal void DebugForceShowUpdateBadge()
		{
			int choice = EditorUtility.DisplayDialogComplex(
				"Debug: v2 Badge Preview",
				"どのクリック動作をテスト？",
				"MDoll URL (⑤⑧)",
				"購入ダイアログ (⑨)",
				"An-Labo URL (⑥)");
			Action? onClick = choice switch
			{
				0 => OpenMDollBoothUrl,
				1 => (Action)ShowPurchaseSelectionDialog,
				2 => OpenAnLaboUpdateUrl,
				_ => null
			};
			var label = this.rootVisualElement.Q<LocalizedLabel>("nail-chip-update-warning");
			if (label == null) return;
			label.style.display = DisplayStyle.Flex;
			this.BindBadgeClickHandler(label, onClick);
		}

		/// <summary>フォルダ内のfbxメッシュのblendshape数でv1/v2を判定 (v2は5個以上、v1は2個)。
		/// GUID共有でv1/v2混在するケースに備え、最初の1個ではなく最大blendshape数で判定する。</summary>
		private static ChipVersion DetectVersionFromFolder(string folder)
		{
			string[] meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { folder });
			int maxBlendShapes = -1;
			foreach (string g in meshGuids)
			{
				string p = AssetDatabase.GUIDToAssetPath(g);
				UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(p);
				foreach (UnityEngine.Object o in assets)
				{
					if (o is Mesh m && m.blendShapeCount > maxBlendShapes)
					{
						maxBlendShapes = m.blendShapeCount;
					}
				}
			}
			if (maxBlendShapes < 0) return ChipVersion.NotInstalled;
			return maxBlendShapes >= 5 ? ChipVersion.V2 : ChipVersion.V1;
		}


		private void PrepareOnCreateGUI()
		{
			MDNailToolUsageStats.Migrate();
			INailProcessor.ClearPreviewMaterialCash();
			this.CleanupScenePreview();
		}
		private void BuildRootUI()
		{
			var uss = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(MDNailToolGuids.WindowUss, MDNailToolGuids.WindowUssPath);
			if (uss != null)
			{
				this.rootVisualElement.styleSheets.Add(uss);
			}

			var uxml = MDNailToolAssetLoader.LoadByGuid<VisualTreeAsset>(MDNailToolGuids.WindowUxml, MDNailToolGuids.WindowUxmlPath);
			if (uxml != null)
			{
				uxml.CloneTree(this.rootVisualElement);
			}
		}
	}
}

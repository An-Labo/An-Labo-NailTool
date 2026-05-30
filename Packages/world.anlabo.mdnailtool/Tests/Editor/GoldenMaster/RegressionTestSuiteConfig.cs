using UnityEngine;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Tests.GoldenMaster
{
	[CreateAssetMenu(fileName = "RegressionTestSuiteConfig", menuName = "MD NailTool/Regression Test Suite Config")]
	public class RegressionTestSuiteConfig : ScriptableObject
	{
		[Header("Input Range (OFF: 各軸1パターン default / ON: 全網羅)")]
		public bool AllAvatars = false;
		public bool AllShapes = false;
		public bool AllDesigns = false;
		public bool AllColors = false;
		public bool AllMaterials = false;

		[Header("Process Flags (OFF: false固定 / ON: false+true 2パターン)")]
		public bool VaryUseFootNail = false;
		public bool VaryRemoveCurrentNail = false;
		public bool VaryGenerateMaterial = false;
		public bool VaryForModularAvatar = false;
		public bool VaryGenerateExpressionMenu = false;
		public bool VarySplitHandFoot = false;
		public bool VaryMergeAnLabo = false;
		public bool VaryArmatureScaleCompensation = false;
		public bool VaryBakeBlendShapes = false;
		public bool VarySyncBlendShapesWithMA = false;
		public bool VaryEnablePenetrationCorrection = false;
		public bool VaryEnableAdditionalMaterials = false;

		[Header("Override Patterns (OFF: null / ON: null + 指定 2パターン)")]
		public bool VaryOverrideMesh = false;
		public bool VaryOverrideMaterial = false;
		public bool VaryPerFingerAdditionalMaterials = false;
		public bool VaryPerFingerAdditionalObjects = false;
		public bool VaryBlendShapeVariant = false;

		[Header("Finger Patterns")]
		public bool VaryFingerPatterns = false;

		[Header("Mode")]
		[Tooltip("ON: 結果を baseline JSON に保存. OFF: baseline と比較してregression検出")]
		public bool UpdateBaseline = false;

		[Tooltip("Baseline JSON 保存先 (Project 相対). default: Tests/Editor/GoldenMaster/Baseline")]
		public string BaselineDir = "Packages/world.anlabo.mdnailtool/Tests/Editor/GoldenMaster/Baseline";

		[Header("Default Single Pattern (各 OFF 時に使われる値)")]
		public string DefaultAvatarPrefabPath = "Assets/SELESTIA/Prefab/SELESTIA_UTS.prefab";
		public string DefaultShapeName = "Natural";
		public string DefaultDesignName = "WiredNail";
	}
}

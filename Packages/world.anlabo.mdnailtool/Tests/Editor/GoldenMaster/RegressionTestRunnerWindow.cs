using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Model;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Tests.GoldenMaster
{
	public class RegressionTestRunnerWindow : EditorWindow, ICallbacks
	{
		private const string TestFixtureFullName = "world.anlabo.mdnailtool.Editor.Tests.GoldenMaster.RegressionTests";

		private RegressionTestSuiteConfig? _config;
		private UnityEditor.Editor? _configEditor;
		private TestRunnerApi? _api;
		private Vector2 _scroll;

		private int _passCount;
		private int _failCount;
		private int _ignoredCount;
		private string _lastResult = "Not run yet";
		private bool _isRunning;

		[MenuItem("Tools/MD NailTool/Regression Test Runner")]
		private static void Open()
		{
			RegressionTestRunnerWindow w = GetWindow<RegressionTestRunnerWindow>("Regression Test Runner");
			w.minSize = new Vector2(520, 700);
		}

		private void OnEnable()
		{
			LoadConfig();
		}

		private void OnDisable()
		{
			if (_api != null) _api.UnregisterCallbacks(this);
			if (_configEditor != null) DestroyImmediate(_configEditor);
		}

		private void LoadConfig()
		{
			string[] guids = AssetDatabase.FindAssets("t:RegressionTestSuiteConfig");
			if (guids.Length == 0) return;
			string path = AssetDatabase.GUIDToAssetPath(guids[0]);
			_config = AssetDatabase.LoadAssetAtPath<RegressionTestSuiteConfig>(path);
			if (_config != null)
			{
				if (_configEditor != null) DestroyImmediate(_configEditor);
				_configEditor = UnityEditor.Editor.CreateEditor(_config);
			}
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("MD NailTool Regression Test Runner", EditorStyles.boldLabel);
			EditorGUILayout.Space();

			if (_config == null)
			{
				EditorGUILayout.HelpBox(
					"RegressionTestSuiteConfig.asset が見つからない. Assets > Create > MD NailTool > Regression Test Suite Config で作成して 'Reload' を押す.",
					MessageType.Warning);
				if (GUILayout.Button("Reload")) LoadConfig();
				return;
			}

			using EditorGUILayout.ScrollViewScope scope = new(_scroll);
			_scroll = scope.scrollPosition;

			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				_configEditor?.OnInspectorGUI();
			}

			EditorGUILayout.Space();
			int estimated = EstimateCases();
			EditorGUILayout.LabelField($"Estimated cases: {estimated:N0}", EditorStyles.boldLabel);
			if (estimated > 10000)
			{
				EditorGUILayout.HelpBox(
					$"パターン数が大きい ({estimated:N0}). Baseline 取得に時間がかかる可能性. 軸を絞ることを推奨.",
					MessageType.Warning);
			}

			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(_isRunning))
			{
				if (GUILayout.Button("Capture Baseline (UpdateBaseline=true で Run All)", GUILayout.Height(28)))
				{
					_config.UpdateBaseline = true;
					EditorUtility.SetDirty(_config);
					AssetDatabase.SaveAssets();
					RunTests();
				}
				if (GUILayout.Button("Verify (UpdateBaseline=false で Run All, 差分検出)", GUILayout.Height(28)))
				{
					_config.UpdateBaseline = false;
					EditorUtility.SetDirty(_config);
					AssetDatabase.SaveAssets();
					RunTests();
				}
			}

			if (GUILayout.Button("Open Test Runner Window (詳細結果確認用)"))
			{
				EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Last Result:", EditorStyles.boldLabel);
			MessageType msgType = _isRunning ? MessageType.Info
				: _failCount > 0 ? MessageType.Error
				: _passCount > 0 ? MessageType.Info : MessageType.None;
			EditorGUILayout.HelpBox(_lastResult, msgType);
		}

		private int EstimateCases()
		{
			int avatars = _config!.AllAvatars ? CountAvatars() : 1;
			int shapes = _config.AllShapes ? 6 : 1;
			int designs = _config.AllDesigns ? 191 : 1;
			int designColors = _config.AllColors ? 5 : 1;
			int designMats = _config.AllMaterials ? 2 : 1;
			int flagBits =
				(_config.VaryUseFootNail ? 1 : 0) +
				(_config.VaryRemoveCurrentNail ? 1 : 0) +
				(_config.VaryGenerateMaterial ? 1 : 0) +
				(_config.VaryForModularAvatar ? 1 : 0) +
				(_config.VaryGenerateExpressionMenu ? 1 : 0) +
				(_config.VarySplitHandFoot ? 1 : 0) +
				(_config.VaryMergeAnLabo ? 1 : 0) +
				(_config.VaryArmatureScaleCompensation ? 1 : 0) +
				(_config.VaryBakeBlendShapes ? 1 : 0) +
				(_config.VarySyncBlendShapesWithMA ? 1 : 0) +
				(_config.VaryEnablePenetrationCorrection ? 1 : 0) +
				(_config.VaryEnableAdditionalMaterials ? 1 : 0);
			return avatars * shapes * designs * designColors * designMats * (1 << flagBits);
		}

		private static int CountAvatars()
		{
			using DBShop db = new();
			int count = 0;
			foreach (Entity.Shop shop in db.collection)
			{
				if (shop.Avatars == null) continue;
				foreach (Entity.Avatar a in shop.Avatars.Values)
				{
					count += a.AvatarVariations?.Count ?? 0;
				}
			}
			return count;
		}

		private void RunTests()
		{
			_passCount = 0;
			_failCount = 0;
			_ignoredCount = 0;
			_isRunning = true;
			_lastResult = "Running...";

			if (_api == null)
			{
				_api = CreateInstance<TestRunnerApi>();
			}
			_api.RegisterCallbacks(this);

			Filter filter = new()
			{
				testMode = TestMode.EditMode,
				groupNames = new[] { TestFixtureFullName }
			};
			_api.Execute(new ExecutionSettings(filter));
		}

		public void RunStarted(ITestAdaptor testsToRun) { }
		public void TestStarted(ITestAdaptor test) { }

		public void TestFinished(ITestResultAdaptor result)
		{
			if (result.Test.IsSuite) return;
			switch (result.TestStatus)
			{
				case TestStatus.Passed: _passCount++; break;
				case TestStatus.Failed: _failCount++; break;
				default: _ignoredCount++; break;
			}
		}

		public void RunFinished(ITestResultAdaptor result)
		{
			_isRunning = false;
			_lastResult = $"PASS: {_passCount} / FAIL: {_failCount} / IGNORED: {_ignoredCount}";
			Repaint();
		}
	}
}

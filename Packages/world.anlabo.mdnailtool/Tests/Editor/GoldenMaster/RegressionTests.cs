using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using Avatar = world.anlabo.mdnailtool.Editor.Entity.Avatar;
using Object = UnityEngine.Object;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Tests.GoldenMaster
{
	public class RegressionTests
	{
		private const string ConfigSearchFilter = "t:RegressionTestSuiteConfig";
		private const string NailPrefabSearchScope = "Assets/[An-Labo.Virtual]/An-Labo Nail Tool/Resource/Nail/Prefab";

		public class FlagSet
		{
			public bool UseFootNail;
			public bool RemoveCurrentNail = true;
			public bool GenerateMaterial = true;
			public bool Backup; // 強制 false. テスト内で asset 書換禁止
			public bool ForModularAvatar;
			public bool GenerateExpressionMenu;
			public bool SplitHandFoot;
			public bool MergeAnLabo;
			public bool ArmatureScaleCompensation;
			public bool BakeBlendShapes;
			public bool SyncBlendShapesWithMA;
			public bool EnablePenetrationCorrection;
			public bool EnableAdditionalMaterials = true;

			public FlagSet Clone() => (FlagSet)MemberwiseClone();

			// 12 フラグの T/F を 12 文字に圧縮. 順序固定で manifest 不要 (FLAG_ORDER 参照)
			public string ComputeLabel()
			{
				char[] chars = new char[12];
				chars[0] = UseFootNail ? 'T' : 'F';
				chars[1] = RemoveCurrentNail ? 'T' : 'F';
				chars[2] = GenerateMaterial ? 'T' : 'F';
				chars[3] = ForModularAvatar ? 'T' : 'F';
				chars[4] = GenerateExpressionMenu ? 'T' : 'F';
				chars[5] = SplitHandFoot ? 'T' : 'F';
				chars[6] = MergeAnLabo ? 'T' : 'F';
				chars[7] = ArmatureScaleCompensation ? 'T' : 'F';
				chars[8] = BakeBlendShapes ? 'T' : 'F';
				chars[9] = SyncBlendShapesWithMA ? 'T' : 'F';
				chars[10] = EnablePenetrationCorrection ? 'T' : 'F';
				chars[11] = EnableAdditionalMaterials ? 'T' : 'F';
				return "F-" + new string(chars);
			}
		}

		// FlagSet.ComputeLabel の 12 文字列の各 index 対応フラグ. JSON ファイル名解読用
		public const string FLAG_ORDER = "UseFootNail,RemoveCurrentNail,GenerateMaterial,ForModularAvatar,GenerateExpressionMenu,SplitHandFoot,MergeAnLabo,ArmatureScaleCompensation,BakeBlendShapes,SyncBlendShapesWithMA,EnablePenetrationCorrection,EnableAdditionalMaterials";

		public class TestCaseInput
		{
			public string AvatarPrefabPath = "";
			public string AvatarLabel = "";
			public string VariationNameHint = "";
			public string ShapeName = "";
			public string DesignName = "";
			public string ColorName = "";
			public string MaterialName = "";
			public FlagSet Flags = new();
			public string CaseName = "";
		}

		private static RegressionTestSuiteConfig LoadOrCreateConfig()
		{
			string[] guids = AssetDatabase.FindAssets(ConfigSearchFilter);
			if (guids.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
				RegressionTestSuiteConfig? cfg = AssetDatabase.LoadAssetAtPath<RegressionTestSuiteConfig>(path);
				if (cfg != null) return cfg;
			}
			return ScriptableObject.CreateInstance<RegressionTestSuiteConfig>();
		}

		// TestCaseSource は static で評価される. config の値を読んで全パターン enumerate
		public static IEnumerable<TestCaseData> GenerateCases()
		{
			RegressionTestSuiteConfig cfg = LoadOrCreateConfig();

			foreach ((string avatarPath, string avatarLabel) in EnumerateAvatars(cfg))
			foreach (string shape in EnumerateShapes(cfg))
			foreach ((string design, string color, string material) in EnumerateDesignVariants(cfg))
			foreach (FlagSet flags in EnumerateFlagSets(cfg))
			{
				string caseName = SanitizeFilename($"{avatarLabel}__{shape}__{design}__{color}__{material}__{flags.ComputeLabel()}");
				TestCaseInput input = new()
				{
					AvatarPrefabPath = avatarPath,
					AvatarLabel = avatarLabel,
					ShapeName = shape,
					DesignName = design,
					ColorName = color,
					MaterialName = material,
					Flags = flags,
					CaseName = caseName,
				};
				yield return new TestCaseData(input).SetName(caseName);
			}
		}

		[Test, TestCaseSource(nameof(GenerateCases))]
		public void Regression(TestCaseInput input)
		{
			RegressionTestSuiteConfig cfg = LoadOrCreateConfig();

			GameObject? avatarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(input.AvatarPrefabPath);
			if (avatarPrefab == null)
			{
				Assert.Ignore($"Avatar prefab not found: {input.AvatarPrefabPath}");
				return;
			}

			GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(avatarPrefab);
			try
			{
				VRCAvatarDescriptor? descriptor = instance.GetComponent<VRCAvatarDescriptor>();
				if (descriptor == null)
				{
					Assert.Ignore($"VRCAvatarDescriptor missing on {input.AvatarPrefabPath}");
					return;
				}

				AvatarMatching matching = new(descriptor);
				var matchResult = matching.Match();
				if (matchResult == null)
				{
					Assert.Ignore($"AvatarMatching failed for {input.AvatarLabel}");
					return;
				}

				(Shop _, Avatar avatar, AvatarVariation variation) = matchResult.Value;

				GameObject? nailPrefab = FindNailPrefab(avatar, variation, input.ShapeName);
				if (nailPrefab == null)
				{
					Assert.Ignore($"Nail prefab missing for {input.AvatarLabel} / {input.ShapeName}");
					return;
				}

				INailProcessor proc = INailProcessor.CreateNailDesign(input.DesignName);
				(INailProcessor, string, string) singleConfig = (proc, input.MaterialName, input.ColorName);
				(INailProcessor, string, string)[] designConfig = Enumerable.Repeat(singleConfig, 20).ToArray();

				NailSetupProcessor processor = new(descriptor, variation, nailPrefab, designConfig, input.ShapeName)
				{
					AvatarName = instance.name,
					UseFootNail = input.Flags.UseFootNail,
					RemoveCurrentNail = input.Flags.RemoveCurrentNail,
					GenerateMaterial = input.Flags.GenerateMaterial,
					Backup = false,
					ForModularAvatar = input.Flags.ForModularAvatar,
					GenerateExpressionMenu = input.Flags.GenerateExpressionMenu,
					SplitHandFoot = input.Flags.SplitHandFoot,
					MergeAnLabo = input.Flags.MergeAnLabo,
					ArmatureScaleCompensation = input.Flags.ArmatureScaleCompensation,
					BakeBlendShapes = input.Flags.BakeBlendShapes,
					SyncBlendShapesWithMA = input.Flags.SyncBlendShapesWithMA,
					EnablePenetrationCorrection = input.Flags.EnablePenetrationCorrection,
					EnableAdditionalMaterials = input.Flags.EnableAdditionalMaterials,
				};

				List<string> capturedWarnings = new();
				List<string> capturedErrors = new();
				System.Action<string>? previousOnLog = ToolConsole.OnLog;
				ToolConsole.OnLog = msg =>
				{
					string normalized = NormalizeLogMessage(msg);
					if (msg.Contains("[Warning]")) capturedWarnings.Add(normalized);
					else if (msg.Contains("[Error]")) capturedErrors.Add(normalized);
					previousOnLog?.Invoke(msg);
				};

				string? processException = null;
				try
				{
					processor.Process();
				}
				catch (System.Exception e)
				{
					processException = $"{e.GetType().Name}: {e.Message}";
				}
				finally
				{
					ToolConsole.OnLog = previousOnLog;
				}

				GoldenMasterSerializer.Snapshot snapshot = GoldenMasterSerializer.Capture(instance.transform, input.CaseName);
				snapshot.Warnings = capturedWarnings;
				snapshot.Errors = capturedErrors;
				snapshot.ProcessException = processException;
				string baselinePath = $"{cfg.BaselineDir}/{input.CaseName}.json";

				if (cfg.UpdateBaseline)
				{
					GoldenMasterSerializer.Save(snapshot, baselinePath);
					Assert.Pass($"Baseline saved: {baselinePath}");
				}
				else
				{
					GoldenMasterSerializer.Snapshot? baseline = GoldenMasterSerializer.Load(baselinePath);
					if (baseline == null)
					{
						Assert.Ignore($"Baseline missing: {baselinePath}. Run with UpdateBaseline=true first.");
						return;
					}
					GoldenMasterDiff.Report diff = GoldenMasterDiff.Compare(baseline, snapshot);
					if (diff.HasAnyDifference)
					{
						Assert.Fail($"Regression detected for {input.CaseName}\n{GoldenMasterDiff.FormatReport(diff, "baseline", "after")}");
					}
				}
			}
			finally
			{
				if (instance != null) Object.DestroyImmediate(instance);
			}
		}

		private static IEnumerable<(string path, string label)> EnumerateAvatars(RegressionTestSuiteConfig cfg)
		{
			if (!cfg.AllAvatars)
			{
				yield return (cfg.DefaultAvatarPrefabPath, ExtractLabel(cfg.DefaultAvatarPrefabPath));
				yield break;
			}

			using DBShop dbShop = new();
			HashSet<string> seenPaths = new();
			foreach (Shop shop in dbShop.collection)
			{
				if (shop.Avatars == null) continue;
				foreach (Avatar avatar in shop.Avatars.Values)
				{
					if (avatar.AvatarVariations == null) continue;
					foreach (AvatarVariation variation in avatar.AvatarVariations.Values)
					{
						if (variation.AvatarPrefabs == null || variation.AvatarPrefabs.Length == 0) continue;
						string? guid = variation.AvatarPrefabs[0].PrefabGUID;
						if (string.IsNullOrEmpty(guid)) continue;
						string path = AssetDatabase.GUIDToAssetPath(guid);
						if (string.IsNullOrEmpty(path)) continue;
						if (!seenPaths.Add(path)) continue;
						string label = $"{avatar.AvatarName}__{variation.VariationName}";
						yield return (path, label);
					}
				}
			}
		}

		private static IEnumerable<string> EnumerateShapes(RegressionTestSuiteConfig cfg)
		{
			if (!cfg.AllShapes)
			{
				yield return cfg.DefaultShapeName;
				yield break;
			}
			using DBNailShape dbShape = new();
			foreach (NailShape shape in dbShape.collection)
			{
				yield return shape.ShapeName;
			}
		}

		private static IEnumerable<(string design, string color, string material)> EnumerateDesignVariants(RegressionTestSuiteConfig cfg)
		{
			using DBNailDesign dbDesign = new();
			IEnumerable<NailDesign> designs = cfg.AllDesigns
				? dbDesign.collection
				: dbDesign.collection.Where(d => d.DesignName == cfg.DefaultDesignName);

			foreach (NailDesign design in designs)
			{
				IEnumerable<string> colors = cfg.AllColors && design.ColorVariation != null
					? design.ColorVariation.Keys
					: new[] { design.ColorVariation?.Keys.FirstOrDefault() ?? "" };

				IEnumerable<string> materials = cfg.AllMaterials && design.MaterialVariation != null
					? design.MaterialVariation.Keys
					: new[] { design.MaterialVariation?.Keys.FirstOrDefault() ?? "" };

				foreach (string color in colors)
				foreach (string material in materials)
				{
					yield return (design.DesignName, color, material);
				}
			}
		}

		private static IEnumerable<FlagSet> EnumerateFlagSets(RegressionTestSuiteConfig cfg)
		{
			List<FlagSet> sets = new() { new FlagSet() };
			sets = Expand(sets, cfg.VaryUseFootNail, (s, v) => s.UseFootNail = v);
			sets = Expand(sets, cfg.VaryRemoveCurrentNail, (s, v) => s.RemoveCurrentNail = v);
			sets = Expand(sets, cfg.VaryGenerateMaterial, (s, v) => s.GenerateMaterial = v);
			sets = Expand(sets, cfg.VaryForModularAvatar, (s, v) => s.ForModularAvatar = v);
			sets = Expand(sets, cfg.VaryGenerateExpressionMenu, (s, v) => s.GenerateExpressionMenu = v);
			sets = Expand(sets, cfg.VarySplitHandFoot, (s, v) => s.SplitHandFoot = v);
			sets = Expand(sets, cfg.VaryMergeAnLabo, (s, v) => s.MergeAnLabo = v);
			sets = Expand(sets, cfg.VaryArmatureScaleCompensation, (s, v) => s.ArmatureScaleCompensation = v);
			sets = Expand(sets, cfg.VaryBakeBlendShapes, (s, v) => s.BakeBlendShapes = v);
			sets = Expand(sets, cfg.VarySyncBlendShapesWithMA, (s, v) => s.SyncBlendShapesWithMA = v);
			sets = Expand(sets, cfg.VaryEnablePenetrationCorrection, (s, v) => s.EnablePenetrationCorrection = v);
			sets = Expand(sets, cfg.VaryEnableAdditionalMaterials, (s, v) => s.EnableAdditionalMaterials = v);
			return sets;
		}

		private static List<FlagSet> Expand(List<FlagSet> sets, bool vary, System.Action<FlagSet, bool> setter)
		{
			if (!vary) return sets;
			List<FlagSet> result = new();
			foreach (FlagSet s in sets)
			{
				FlagSet sFalse = s.Clone();
				setter(sFalse, false);
				result.Add(sFalse);

				FlagSet sTrue = s.Clone();
				setter(sTrue, true);
				result.Add(sTrue);
			}
			return result;
		}

		private static GameObject? FindNailPrefab(Avatar avatar, AvatarVariation variation, string shape)
		{
			string[] scope = { NailPrefabSearchScope };
			string[] queries = { variation.VariationName, avatar.AvatarName };

			foreach (string query in queries)
			{
				if (string.IsNullOrEmpty(query)) continue;
				string[] guids = AssetDatabase.FindAssets($"t:Prefab {query}", scope);
				foreach (string guid in guids)
				{
					string path = AssetDatabase.GUIDToAssetPath(guid);
					string fileName = Path.GetFileName(path);
					if (fileName.Contains($"[{shape}]") &&
						fileName.Contains(query, System.StringComparison.OrdinalIgnoreCase))
					{
						return AssetDatabase.LoadAssetAtPath<GameObject>(path);
					}
				}
			}

			foreach (string query in queries)
			{
				if (string.IsNullOrEmpty(query)) continue;
				string[] guids = AssetDatabase.FindAssets($"t:Prefab {query}", scope);
				foreach (string guid in guids)
				{
					string path = AssetDatabase.GUIDToAssetPath(guid);
					if (path.Contains(query, System.StringComparison.OrdinalIgnoreCase))
					{
						return AssetDatabase.LoadAssetAtPath<GameObject>(path);
					}
				}
			}
			return null;
		}

		private static string ExtractLabel(string path)
		{
			string name = Path.GetFileNameWithoutExtension(path);
			return string.IsNullOrEmpty(name) ? "Unknown" : name;
		}

		private static readonly char[] InvalidFileChars = Path.GetInvalidFileNameChars();
		private static string SanitizeFilename(string s)
		{
			return string.Concat(s.Select(c => System.Array.IndexOf(InvalidFileChars, c) >= 0 ? '_' : c));
		}

		// ToolConsole.Log は先頭にタイムスタンプを付ける. 比較のため除去
		private static readonly System.Text.RegularExpressions.Regex TimestampRegex =
			new(@"^\[\d{2}:\d{2}:\d{2}\]\s*");

		private static string NormalizeLogMessage(string msg)
		{
			return TimestampRegex.Replace(msg, "");
		}
	}
}

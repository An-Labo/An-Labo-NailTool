#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
				bool maEnabled = GlobalSetting.UseModularAvatar;
				IEnumerable<AvatarBlendShapeVariant> filtered = maEnabled
					? variants
					: variants.Where(v => !v.Name.StartsWith("Shrink_", StringComparison.OrdinalIgnoreCase));
				choices.AddRange(filtered.Select(v => v.Name));
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
			bool bakeEnabled = maEnabled && GlobalSetting.BakeBlendShapes;

			bool hasVariants = popup.choices.Count > 1;

			popup.style.display = DisplayStyle.Flex;

			if (bakeEnabled)
			{
				popup.SetEnabled(false);
				popup.choices = new List<string> { S("window.blendshape_variant_bake_active") ?? "BlendShape generation is enabled" };
				popup.index = 0;
			}
			else if (!hasVariants)
			{
				popup.SetEnabled(false);
				popup.choices = new List<string> { S("window.blendshape_variant_none") ?? "No BlendShape" };
				popup.index = 0;
			}
			else
			{
				popup.SetEnabled(true);
			}

			this.UpdateBakeBlendShapeGeneratedList(bakeEnabled);
		}

		private void UpdateBakeBlendShapeGeneratedList(bool? bakeEnabledOverride = null)
		{
			if (this._bakeBlendShapeGeneratedList == null) return;
			bool bakeEnabled = bakeEnabledOverride ?? (GlobalSetting.UseModularAvatar && GlobalSetting.BakeBlendShapes);
			if (!bakeEnabled)
			{
				this._bakeBlendShapeGeneratedList.text = string.Empty;
				this._bakeBlendShapeGeneratedList.style.display = DisplayStyle.None;
				return;
			}

			List<string> names = this.GetGeneratedBlendShapeNames();
			string text = names.Count > 0
				? string.Format(S("window.bake_blendshape_generated_list") ?? "生成されるBlendShape: {0}", string.Join(", ", names))
				: S("window.bake_blendshape_generated_list_empty") ?? "生成されるBlendShapeはありません";
			this._bakeBlendShapeGeneratedList.text = text;
			this._bakeBlendShapeGeneratedList.style.display = DisplayStyle.Flex;
		}

		private List<string> GetGeneratedBlendShapeNames()
		{
			var result = new List<string>();
			AvatarBlendShapeVariant[]? variants = this.GetCurrentBlendShapeVariants();
			if (variants == null) return result;

			foreach (AvatarBlendShapeVariant variant in variants)
			{
				bool isShrink = IsShrinkBlendShapeVariant(variant);
				if (isShrink)
				{
					if (GlobalSetting.AutoLinkShrinkBS && !string.IsNullOrEmpty(variant.Name)) result.Add(variant.Name);
					continue;
				}

				if (string.IsNullOrEmpty(variant.NailPrefabGUID) && (variant.NailNodes == null || variant.NailNodes.Length == 0)) continue;
				if (!string.IsNullOrEmpty(variant.LeftBlendShapeName) && !string.IsNullOrEmpty(variant.RightBlendShapeName))
				{
					result.Add(variant.LeftBlendShapeName!);
					result.Add(variant.RightBlendShapeName!);
				}
				else if (!string.IsNullOrEmpty(variant.Name))
				{
					result.Add(variant.Name);
				}
			}

			return result;
		}

		private AvatarBlendShapeVariant[]? GetCurrentBlendShapeVariants()
		{
			if (this._avatarDropDowns == null) return null;
			AvatarVariation? avatarVariationData = this._avatarDropDowns.GetSelectedAvatarVariation();
			AvatarBlendShapeVariant[]? variants = avatarVariationData?.BlendShapeVariants;
			if (variants != null) return variants;

			using DBShop dbShop = new();
			string avatarName = this._avatarDropDowns.GetAvatarName();
			foreach (Shop s in dbShop.collection)
			{
				Avatar? av = s.FindAvatarByName(avatarName);
				if (av?.BlendShapeVariants != null)
				{
					return av.BlendShapeVariants;
				}
			}
			return null;
		}

		private static bool IsShrinkBlendShapeVariant(AvatarBlendShapeVariant variant)
		{
			return string.IsNullOrEmpty(variant.NailPrefabGUID)
			       && (variant.NailNodes == null || variant.NailNodes.Length == 0)
			       && !string.IsNullOrEmpty(variant.Name)
			       && variant.Name.StartsWith("Shrink_", StringComparison.OrdinalIgnoreCase);
		}

		private void UpdatePreviewAreaVisibility(bool visible)
		{
			var area = this.rootVisualElement.Q<VisualElement>("nail-preview-area");
			if (area != null)
				area.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void UpdateToolConsoleVisibility(bool visible)
		{
			if (this._toolConsoleContainer != null)
				this._toolConsoleContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}


		private void AppendConsoleLog(string message)
		{
			if (this._toolConsoleScroll == null) return;
			var label = new Label(message);
			label.AddToClassList("mdn-tool-console-entry");
			this._toolConsoleScroll.Add(label);

			// 自動スクロール
			this._toolConsoleScroll.schedule.Execute(() =>
			{
				this._toolConsoleScroll.scrollOffset = new Vector2(0, float.MaxValue);
			});
		}

		private string BuildToolConsoleLog()
		{
			if (this._toolConsoleScroll == null) return string.Empty;
			var lines = this._toolConsoleScroll.Children()
				.OfType<Label>()
				.Select(l => l.text)
				.Where(line => !string.IsNullOrWhiteSpace(line))
				.ToArray();
			if (lines.Length == 0) return string.Empty;

			var sb = new System.Text.StringBuilder();
			sb.AppendLine();
			sb.AppendLine("--- NailTool Log ---");
			foreach (string line in lines)
			{
				sb.AppendLine(line);
			}
			return sb.ToString();
		}

		private string BuildConsoleDiagnosticInfo()
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("--- An-Labo NailTool Support Info ---");

			try { sb.AppendLine($"NailTool Version: {MDNailToolDefines.Version}"); }
			catch (Exception ex) { sb.AppendLine($"NailTool Version: (取得失敗: {ex.Message})"); }

			sb.AppendLine($"Unity: {Application.unityVersion}");
			sb.AppendLine($"OS: {SystemInfo.operatingSystem}");

			try
			{
				string packageJsonPath = "Packages/nadena.dev.modular-avatar/package.json";
				TextAsset? packageJson = MDNailToolAssetLoader.LoadAssetSafe<TextAsset>(packageJsonPath);
				sb.AppendLine($"ModularAvatar: {packageJson?.text switch { string t => Newtonsoft.Json.Linq.JObject.Parse(t)["version"]?.ToString() ?? "unknown", _ => "not installed" }}");
			}
			catch (Exception ex) { sb.AppendLine($"ModularAvatar: (取得失敗: {ex.Message})"); }

			var avatar = this._avatarObjectField?.value as VRCAvatarDescriptor;
			sb.AppendLine($"Avatar: {avatar?.gameObject?.name ?? "(null)"}");
			sb.AppendLine($"Avatar Root Scale: {avatar?.transform?.localScale.ToString() ?? "(null)"}");
			AppendArmatureScaleInfo(sb, avatar);
			sb.AppendLine($"AvatarName: {this._avatarDropDowns?.GetAvatarName() ?? "(未設定)"}");
			sb.AppendLine($"Variation: {this._avatarDropDowns?.GetSelectedAvatarVariation()?.VariationName ?? "(null)"}");
			sb.AppendLine($"NailShape: {this._nailShapeDropDown?.value ?? "(null)"}");
			{
				GameObject? diagPrefab = this._avatarDropDowns?.GetSelectedPrefab();
				sb.AppendLine($"NailPrefab: {diagPrefab?.name ?? "(null)"}");
				if (diagPrefab != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(diagPrefab)))
				{
					Object.DestroyImmediate(diagPrefab);
				}
			}
			sb.AppendLine($"ForModularAvatar: {this._forModularAvatar?.value}");
			sb.AppendLine($"BakeBlendShapes: {this._bakeBlendShapes?.value}");
			sb.AppendLine($"SyncBlendShapesWithMA: {this._syncBlendShapesWithMA?.value}");
			sb.AppendLine($"ArmatureScaleCompensation: {this._armatureScaleCompensation?.value}");
			sb.AppendLine($"UseFootNail: {this._tglFootActive?.value}");
			sb.AppendLine($"HandActive: {this._tglHandActive?.value}");
			sb.AppendLine($"HandDetail: {this._tglHandDetail?.value}");
			sb.AppendLine($"FootDetail: {this._tglFootDetail?.value}");
			sb.AppendLine($"AdditionalObjectSource: {this._additionalObjectSourceDropdown?.value ?? "(null)"}");
			sb.AppendLine($"AdditionalMaterialSource: {this._additionalMaterialSourceDropdown?.value ?? "(null)"}");

			AppendUnityConsoleMessages(sb);

			// Body BlendShape状態（値が0でないもののみ）
			if (avatar != null)
			{
				try
				{
					SkinnedMeshRenderer? visemeSmr = avatar.VisemeSkinnedMesh;
					var bodySmr = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
						.Where(smr => smr != visemeSmr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
						.OrderByDescending(smr => smr.sharedMesh!.blendShapeCount)
						.FirstOrDefault();
					if (bodySmr != null && bodySmr.sharedMesh != null)
					{
						sb.AppendLine($"--- Body BlendShapes ({bodySmr.gameObject.name}) ---");
						Mesh mesh = bodySmr.sharedMesh;
						bool hasNonZero = false;
						for (int i = 0; i < mesh.blendShapeCount; i++)
						{
							float weight = bodySmr.GetBlendShapeWeight(i);
							if (weight != 0f)
							{
								sb.AppendLine($"  {mesh.GetBlendShapeName(i)}: {weight:F1}");
								hasNonZero = true;
							}
						}
						if (!hasNonZero) sb.AppendLine("  (all zero)");
					}
				}
				catch (Exception ex) { ToolConsole.Warn("NailTool", $"BlendShape診断情報の取得に失敗: {ex.Message}"); }
			}

			// ビルド診断（直近のPlayモード/ビルド時の結果、AAOProcessorが収集）
			if (!string.IsNullOrEmpty(AAOProcessor.LastBuildDiagnostic))
			{
				sb.AppendLine("--- Build Diagnostic ---");
				sb.Append(AAOProcessor.LastBuildDiagnostic);
			}
			else
			{
				sb.AppendLine("--- Build Diagnostic ---");
				sb.AppendLine("(no build data — run Play mode first)");
			}

			return sb.ToString();
		}

		private void AppendArmatureScaleInfo(System.Text.StringBuilder sb, VRCAvatarDescriptor? avatar)
		{
			if (avatar == null)
			{
				sb.AppendLine("A:null");
				return;
			}

			Transform? armature = FindArmatureRoot(avatar);
			if (armature == null)
			{
				sb.AppendLine("A:not found");
				return;
			}

			sb.AppendLine($"A:{GetRelativePath(avatar.transform, armature)} p={FormatVector3(armature.localPosition)} r={FormatVector3(armature.localEulerAngles)} s={FormatVector3(armature.localScale)}");
			AppendArmatureCompensationPoints(sb, avatar);
		}

		private void AppendArmatureCompensationPoints(System.Text.StringBuilder sb, VRCAvatarDescriptor avatar)
		{
			try
			{
				Dictionary<string, Transform?> targetBones = NailSetupProcessor.GetTargetBoneDictionary(
					avatar,
					this._avatarDropDowns?.GetSelectedAvatarVariation()?.BoneMappingOverride);
				Dictionary<string, Vector3> referenceScales = GetReferenceBoneScalesByActualName(avatar);

				var treeRoot = new TransformDiagnosticTreeNode();
				AppendTargetBoneDiagnostics(treeRoot, avatar.transform, targetBones, referenceScales, 0, 10);
				if (this._tglFootActive?.value == true)
				{
					AppendTargetBoneDiagnostics(treeRoot, avatar.transform, targetBones, referenceScales, 12, 5);
					AppendTargetBoneDiagnostics(treeRoot, avatar.transform, targetBones, referenceScales, 17, 5);
				}

				sb.AppendLine("ACP:");
				if (treeRoot.Children.Count == 0)
				{
					sb.AppendLine("  none");
					return;
				}
				AppendDiagnosticTreeLines(sb, treeRoot, 1);
			}
			catch (Exception ex)
			{
				sb.AppendLine($"ACP:err {ex.Message}");
			}
		}

		private static void AppendTargetBoneDiagnostics(
			TransformDiagnosticTreeNode treeRoot,
			Transform avatarRoot,
			IReadOnlyDictionary<string, Transform?> targetBones,
			IReadOnlyDictionary<string, Vector3> referenceScales,
			int startIndex,
			int count)
		{
			for (int i = startIndex; i < startIndex + count && i < MDNailToolDefines.TARGET_BONE_NAME_LIST.Count; i++)
			{
				string boneName = MDNailToolDefines.TARGET_BONE_NAME_LIST[i];
				if (!targetBones.TryGetValue(boneName, out Transform? targetBone) || targetBone == null) continue;

				TransformDiagnosticTreeNode node = AddTransformPath(treeRoot, avatarRoot, targetBone);
				Vector3 referenceScale = referenceScales.TryGetValue(targetBone.name, out Vector3 foundReferenceScale)
					? foundReferenceScale
					: Vector3.one;
				Vector3 ratio = ScaleRatio(targetBone.lossyScale, referenceScale);
				node.Entries.Add($"[{TargetAlias(i)}] k={FormatVector3(ratio)} ref={FormatVector3(referenceScale)}");
			}
		}

		private static Dictionary<string, Vector3> GetReferenceBoneScalesByActualName(VRCAvatarDescriptor avatar)
		{
			var result = new Dictionary<string, Vector3>();
			Animator? animator = avatar.GetComponent<Animator>();
			if (animator == null || animator.avatar == null) return result;

			string modelPath = AssetDatabase.GetAssetPath(animator.avatar);
			if (string.IsNullOrEmpty(modelPath)) return result;

			GameObject? referenceAsset = MDNailToolAssetLoader.LoadPrefabSafe(modelPath);
			if (referenceAsset == null) return result;

			GameObject tempInstance = Object.Instantiate(referenceAsset);
			try
			{
				tempInstance.transform.SetPositionAndRotation(avatar.transform.position, avatar.transform.rotation);
				tempInstance.transform.localScale = avatar.transform.lossyScale;
				foreach (Transform t in tempInstance.GetComponentsInChildren<Transform>(true))
				{
					if (!result.ContainsKey(t.name)) result[t.name] = t.lossyScale;
				}
			}
			finally
			{
				Object.DestroyImmediate(tempInstance);
			}

			return result;
		}

		private static TransformDiagnosticTreeNode AddTransformPath(TransformDiagnosticTreeNode root, Transform avatarRoot, Transform target)
		{
			var chain = new Stack<Transform>();
			Transform? current = target;
			while (current != null && current != avatarRoot)
			{
				chain.Push(current);
				current = current.parent;
			}

			TransformDiagnosticTreeNode node = root;
			foreach (Transform transform in chain)
			{
				if (!node.Children.TryGetValue(transform.name, out TransformDiagnosticTreeNode child))
				{
					child = new TransformDiagnosticTreeNode();
					node.Children[transform.name] = child;
				}
				child.Transform = transform;
				node = child;
			}
			return node;
		}

		private static void AppendDiagnosticTreeLines(System.Text.StringBuilder sb, TransformDiagnosticTreeNode node, int depth)
		{
			string indent = new string(' ', depth * 2);
			foreach (KeyValuePair<string, TransformDiagnosticTreeNode> childPair in node.Children)
			{
				TransformDiagnosticTreeNode child = childPair.Value;
				string suffix = child.Transform != null ? $" p={FormatVector3(child.Transform.localPosition)} r={FormatVector3(child.Transform.localEulerAngles)} s={FormatVector3(child.Transform.localScale)}" : "/";
				if (child.Entries.Count > 0) suffix += " " + string.Join(" ", child.Entries);
				sb.AppendLine($"{indent}{childPair.Key}{suffix}");
				AppendDiagnosticTreeLines(sb, child, depth + 1);
			}
		}

		private sealed class TransformDiagnosticTreeNode
		{
			public SortedDictionary<string, TransformDiagnosticTreeNode> Children { get; } = new(StringComparer.Ordinal);
			public Transform? Transform { get; set; }
			public List<string> Entries { get; } = new();
		}

		private static Vector3 ScaleRatio(Vector3 actual, Vector3 reference)
		{
			return new Vector3(SafeScaleRatio(actual.x, reference.x), SafeScaleRatio(actual.y, reference.y), SafeScaleRatio(actual.z, reference.z));
		}

		private static float SafeScaleRatio(float actual, float reference)
		{
			return Mathf.Abs(reference) > 1e-6f ? actual / reference : 1f;
		}

		private static string TargetAlias(int index)
		{
			return index switch
			{
				0 => "LTh",
				1 => "LIn",
				2 => "LMi",
				3 => "LRi",
				4 => "LLi",
				5 => "RTh",
				6 => "RIn",
				7 => "RMi",
				8 => "RRi",
				9 => "RLi",
				12 => "LFTh",
				13 => "LFIn",
				14 => "LFMi",
				15 => "LFRi",
				16 => "LFLi",
				17 => "RFTh",
				18 => "RFIn",
				19 => "RFMi",
				20 => "RFRi",
				21 => "RFLi",
				_ => index.ToString(CultureInfo.InvariantCulture)
			};
		}

		private static Transform? FindArmatureRoot(VRCAvatarDescriptor avatar)
		{
			Transform avatarTransform = avatar.transform;
			foreach (Transform child in avatarTransform)
			{
				if (child.name == "Armature") return child;
			}

			Transform? namedArmature = avatar.GetComponentsInChildren<Transform>(true)
				.FirstOrDefault(t => t.name == "Armature");
			if (namedArmature != null) return namedArmature;

			Animator? animator = avatar.GetComponent<Animator>();
			Transform? hips = animator != null && animator.isHuman
				? animator.GetBoneTransform(HumanBodyBones.Hips)
				: null;
			if (hips == null) return null;

			Transform current = hips;
			while (current.parent != null && current.parent != avatarTransform)
			{
				current = current.parent;
			}
			return current.parent == avatarTransform ? current : null;
		}

		private static string FormatVector3(Vector3 value)
		{
			return string.Format(CultureInfo.InvariantCulture, "({0:0.####},{1:0.####},{2:0.####})", value.x, value.y, value.z);
		}

		private static string GetRelativePath(Transform root, Transform target)
		{
			var names = new Stack<string>();
			Transform? current = target;
			while (current != null && current != root)
			{
				names.Push(current.name);
				current = current.parent;
			}
			return names.Count == 0 ? target.name : string.Join("/", names);
		}
		private static void AppendUnityConsoleMessages(System.Text.StringBuilder sb)
		{
			sb.AppendLine("--- Unity Console Errors/Warnings (newest first) ---");
			try
			{
				Type? logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor");
				Type? logEntryType = Type.GetType("UnityEditor.LogEntry,UnityEditor");
				if (logEntriesType == null || logEntryType == null)
				{
					sb.AppendLine("(Unity Console API unavailable)");
					return;
				}

				const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
				MethodInfo? getCount = logEntriesType.GetMethod("GetCount", flags);
				MethodInfo? getEntryInternal = logEntriesType.GetMethod("GetEntryInternal", flags);
				if (getCount == null || getEntryInternal == null)
				{
					sb.AppendLine("(Unity Console API unavailable)");
					return;
				}

				object? entry = Activator.CreateInstance(logEntryType);
				if (entry == null)
				{
					sb.AppendLine("(Unity Console entry unavailable)");
					return;
				}

				FieldInfo? conditionField = logEntryType.GetField("condition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				FieldInfo? stackTraceField = logEntryType.GetField("stackTrace", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				FieldInfo? modeField = logEntryType.GetField("mode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				logEntriesType.GetMethod("StartGettingEntries", flags)?.Invoke(null, null);
				try
				{
					int count = Convert.ToInt32(getCount.Invoke(null, null));
					int appended = 0;
					for (int i = count - 1; i >= 0 && appended < 20; i--)
					{
						getEntryInternal.Invoke(null, new[] { (object)i, entry });
						string condition = conditionField?.GetValue(entry)?.ToString() ?? string.Empty;
						string stackTrace = stackTraceField?.GetValue(entry)?.ToString() ?? string.Empty;
						int mode = Convert.ToInt32(modeField?.GetValue(entry) ?? 0);
						string level = GetUnityConsoleLevel(mode, condition);
						if (level == "Log") continue;

						sb.AppendLine($"[{level}] {FirstLine(condition)}");
						AppendStackTraceSummary(sb, stackTrace);
						appended++;
					}

					if (appended == 0)
					{
						sb.AppendLine("(no recent errors/warnings)");
					}
				}
				finally
				{
					logEntriesType.GetMethod("EndGettingEntries", flags)?.Invoke(null, null);
				}
			}
			catch (Exception ex)
			{
				sb.AppendLine($"(failed to read Unity Console: {ex.Message})");
			}
		}

		private static string GetUnityConsoleLevel(int mode, string condition)
		{
			const int errorFlags = 1 | 2 | 16 | 64 | 256 | 2048 | 8192 | 131072;
			const int warningFlags = 128 | 512 | 4096;
			if ((mode & errorFlags) != 0) return "Error";
			if ((mode & warningFlags) != 0) return "Warning";
			if (condition.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
			    condition.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return "Error";
			}
			if (condition.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return "Warning";
			}
			return "Log";
		}

		private static string FirstLine(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return "(empty message)";
			string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			return lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim() ?? "(empty message)";
		}

		private static void AppendStackTraceSummary(System.Text.StringBuilder sb, string stackTrace)
		{
			if (string.IsNullOrWhiteSpace(stackTrace)) return;
			string[] lines = stackTrace.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
				.Where(line => !string.IsNullOrWhiteSpace(line))
				.Take(6)
				.ToArray();
			foreach (string line in lines)
			{
				sb.AppendLine("  " + line.Trim());
			}
		}

		private void BindLinksUI()
		{
			var updateNoticeContainer = this.rootVisualElement.Q<VisualElement>("update-notice-container");
			if (updateNoticeContainer != null)
			{
				updateNoticeContainer.Add(new UpdateNoticeBanner());
			}

			// Changelog バナーを動的追加
			var bannerContainer = this.rootVisualElement.Q<VisualElement>("changelog-banner-container");
			if (bannerContainer != null) {
				var banner = new ChangelogBanner();
				bannerContainer.Add(banner);
			}

			this._manualLink = this.rootVisualElement.Q<Label>("link-manual");
			if (this._manualLink != null) {
				this._manualLink.text = $"[{S("link.manual.label") ?? "Manual"}]";
				this._manualLink.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.manual")));
			}

			// ヘッダーのカタログリンク
			var catalogLink = this.rootVisualElement.Q<Label>("link-catalog");
			if (catalogLink != null) {
				catalogLink.text = $"[{S("link.catalog.label") ?? "Catalog"}]";
				catalogLink.RegisterCallback<ClickEvent>(_ => Application.OpenURL(S("link.catalog")));
			}

			// ヘッダーのFAQリンク
			var headerContact = this.rootVisualElement.Q<Label>("link-contact-header");
			headerContact?.RegisterCallback<ClickEvent>(_ => FAQWindow.ShowWindow(this));

			this._largePreviewLink = this.rootVisualElement.Q<Label>("link-large-preview");
			if (this._largePreviewLink != null) {
				this._largePreviewLink.text = S("window.preview_browser_large") ?? "View larger in browser";
				this._largePreviewLink.tooltip = S("tooltip.preview_browser_large") ?? "Open the selected nail in your browser.";
				this._largePreviewLink.RegisterCallback<ClickEvent>(_ => this.OpenSelectedNailInBrowser());
			}

			// 着用統計リンク
			var usageStatsLink = this.rootVisualElement.Q<Label>("link-usage-stats");
			if (usageStatsLink != null)
			{
				usageStatsLink.text = $"[{S("usage_stats.link_label") ?? "Usage Stats"}]";
				usageStatsLink.RegisterCallback<ClickEvent>(_ => UsageStatsWindow.ShowWindow());
			}

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

			// おすすめ設定ボタン
			AddRecommendButton("mdn-section-header", 3, ApplyRecommendMA);
			AddRecommendButtonToFoldout("mdn-advanced-foldout", ApplyRecommendAdvanced);

			// ネイルデザイン (section index 1) ヘッダ: 並び替え → 検索 の順
			AddHeaderButton("mdn-section-header", 1, S("window.sort") ?? "Sort", null, () => this._nailDesignSelect?.ToggleSortMode());
			AddHeaderButton("mdn-section-header", 1, S("window.search_nail") ?? "Search", "d_Search Icon", () => this._nailDesignSelect?.TriggerSearch());

			// ネイル設定 (section index 2) ヘッダ: デフォルト設定に戻す
			AddHeaderButton("mdn-section-header", 2, S("window.reset_to_default") ?? "Reset", null, ApplyResetNailSettings);

			// 詳細設定Foldoutの開閉状態を記憶する
			const string advancedFoldoutPrefKey = "MDNailTool.AdvancedFoldoutOpen";
			var advancedFoldout = this.rootVisualElement.Q<Foldout>(className: "mdn-advanced-foldout");
			if (advancedFoldout != null)
			{
				advancedFoldout.SetValueWithoutNotify(EditorPrefs.GetBool(advancedFoldoutPrefKey, false));
				advancedFoldout.RegisterValueChangedCallback(evt =>
				{
					EditorPrefs.SetBool(advancedFoldoutPrefKey, evt.newValue);
				});
			}
		}

		private Button CreateRecommendButton(System.Action onClick)
		{
			string label = S("window.recommend") ?? "Recommended";
			var btn = new Button(onClick) { text = label };
			btn.style.height = 20;
			btn.style.fontSize = 10;
			btn.style.paddingLeft = 8;
			btn.style.paddingRight = 8;
			btn.style.marginLeft = new StyleLength(StyleKeyword.Auto);
			return btn;
		}

		private Button CreateHeaderButton(string label, string? iconName, System.Action onClick)
		{
			var btn = new Button(onClick);
			btn.style.height = 20;
			btn.style.paddingLeft = 8;
			btn.style.paddingRight = 8;
			btn.style.marginLeft = 4;
			btn.style.flexDirection = FlexDirection.Row;
			btn.style.alignItems = Align.Center;
			btn.style.flexShrink = 0;
			if (!string.IsNullOrEmpty(iconName))
			{
				var icon = new Image { image = EditorGUIUtility.Load(iconName) as Texture2D };
				icon.style.width = 12; icon.style.height = 12; icon.style.marginRight = 3;
				icon.tintColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : Color.black;
				icon.pickingMode = PickingMode.Ignore;
				btn.Add(icon);
			}
			var lbl = new Label(label) {
				style = {
					fontSize = 10,
					whiteSpace = WhiteSpace.NoWrap,
					unityTextAlign = TextAnchor.MiddleCenter,
					paddingTop = 0, paddingBottom = 0, paddingLeft = 0, paddingRight = 0,
				}
			};
			lbl.pickingMode = PickingMode.Ignore;
			btn.Add(lbl);
			return btn;
		}

		private void AddHeaderButton(string headerClass, int headerIndex, string label, string? iconName, System.Action onClick)
		{
			var headers = this.rootVisualElement.Query(className: headerClass).ToList();
			if (headerIndex >= headers.Count) return;
			var header = headers[headerIndex];
			header.style.flexDirection = FlexDirection.Row;
			header.style.alignItems = Align.Center;
			// 右寄せ Spacer (1度だけ追加). 以降のボタンは隣接表示.
			const string SPACER_NAME = "mdn-header-spacer";
			if (header.Q<VisualElement>(SPACER_NAME) == null)
			{
				var spacer = new VisualElement { name = SPACER_NAME };
				spacer.style.flexGrow = 1;
				header.Add(spacer);
			}
			var btn = CreateHeaderButton(label, iconName, onClick);
			header.Add(btn);
		}

		private void ApplyResetNailSettings()
		{
			if (this._tglHandActive != null) this._tglHandActive.value = true;
			if (this._tglFootActive != null) this._tglFootActive.value = true;
			if (this._tglHandDetail != null) this._tglHandDetail.value = false;
			if (this._tglFootDetail != null) this._tglFootDetail.value = false;
			// 指ごと有効化 (bulk 全 ON で 20 指すべて enabled に揃える).
			if (this._bulkLeftHand != null) this._bulkLeftHand.value = true;
			if (this._bulkRightHand != null) this._bulkRightHand.value = true;
			if (this._bulkLeftFoot != null) this._bulkLeftFoot.value = true;
			if (this._bulkRightFoot != null) this._bulkRightFoot.value = true;
			// 追加マテリアル / 追加オブジェクトは現在の選択を維持 (ネイルに設定されてる値を尊重).
		}

		private void AddRecommendButton(string headerClass, int headerIndex, System.Action onClick)
		{
			var headers = this.rootVisualElement.Query(className: headerClass).ToList();
			if (headerIndex < headers.Count)
			{
				var header = headers[headerIndex];
				header.style.flexDirection = FlexDirection.Row;
				header.style.justifyContent = Justify.SpaceBetween;
				header.style.alignItems = Align.Center;
				header.Add(CreateRecommendButton(onClick));
			}
		}

		private void AddRecommendButtonToFoldout(string foldoutClass, System.Action onClick)
		{
			var foldout = this.rootVisualElement.Q(className: foldoutClass);
			if (foldout == null) return;

			var toggle = foldout.Q<Toggle>(className: "unity-foldout__toggle");
			if (toggle == null) return;

			toggle.style.flexDirection = FlexDirection.Row;
			toggle.style.justifyContent = Justify.SpaceBetween;
			toggle.style.alignItems = Align.Center;
			toggle.Add(CreateRecommendButton(onClick));
		}

		private void ApplyRecommendMA()
		{
			if (this._forModularAvatar != null) this._forModularAvatar.value = true;
			if (this._generateExpressionMenu != null) this._generateExpressionMenu.value = true;
			if (this._splitHandFootExpressionMenu != null) this._splitHandFootExpressionMenu.value = true;
			if (this._mergeAnLaboExpressionMenu != null) this._mergeAnLaboExpressionMenu.value = true;
			if (this._bakeBlendShapes != null) this._bakeBlendShapes.value = true;
			if (this._syncBlendShapesWithMA != null) this._syncBlendShapesWithMA.value = true;
		}

		private void ApplyRecommendAdvanced()
		{
			// ON
			if (this._removeCurrentNail != null) this._removeCurrentNail.value = true;
			if (this._backup != null) this._backup.value = true;
			if (this._armatureScaleCompensation != null) this._armatureScaleCompensation.value = true;

			// OFF
			if (this._enableDirectMaterial != null) this._enableDirectMaterial.value = false;
			if (this._penetrationCorrection != null) this._penetrationCorrection.value = false;

			// 試着トグルはOFF固定 (毎回OFF運用)
			this._tryoutActive = false;
			GlobalSetting.EnableSceneWearingPreview = false;
			this.UpdateTryoutVisual();
		}

	}
}

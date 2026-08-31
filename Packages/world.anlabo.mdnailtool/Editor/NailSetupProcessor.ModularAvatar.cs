using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Runtime;
using Object = UnityEngine.Object;

#if MD_NAIL_FOR_MA
using nadena.dev.modular_avatar.core;
#endif

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public partial class NailSetupProcessor {
		private void SetupForModularAvatar(
			GameObject nailPrefabObject,
			Dictionary<string, Transform?> targetBoneDictionary,
			Transform?[] handsNailObjects,
			Transform?[] leftFootNailObjects,
			Transform?[] rightFootNailObjects,
			List<(SkinnedMeshRenderer sourceSmr, string sourcePath)> resolvedSourceSmrs,
			Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>? corrections) {
#if MD_NAIL_FOR_MA
				string variationName = this.AvatarVariationData.VariationName;
				string handWrapperName = $"HandNail_{variationName}";
				string footWrapperName = $"FootNail_{variationName}";

				// ---- BakeBlendShapes=trueの場合: 先にボーン位置に配置してからCombine ----
				GameObject? handCombinedGo = null;
				GameObject? footCombinedGo = null;
				if (this.BakeBlendShapes)
				{
					string avatarAssetName = !string.IsNullOrEmpty(this.AvatarName)
						? this.AvatarName!
						: this.Avatar.gameObject.name;
					string avatarObjectName = this.Avatar.gameObject.name ?? avatarAssetName;
					string bsPath = $"{MDNailToolDefines.GENERATED_ASSET_PATH}CombinedMesh/{SanitizeAssetPathSegment(avatarAssetName)}/{SanitizeAssetPathSegment(avatarObjectName)}";

					// ターゲットボーンへの親設定
					int bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
					foreach (Transform? nailObject in handsNailObjects)
					{
						bpIndex++;
						if (nailObject == null) continue;
						Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
						if (targetBone == null) continue;
						Vector3? desired = null;
						if (corrections != null && corrections.TryGetValue(nailObject, out var c))
						{
							nailObject.position = c.position;
							nailObject.rotation = c.rotation;
							desired = c.desiredLossyScale;
						}
						nailObject.SetParent(targetBone, true);
						if (desired.HasValue) EnforceLossyScale(nailObject, desired.Value);
					}
					if (this.UseFootNail)
					{
						bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
						foreach (Transform? nailObject in leftFootNailObjects)
						{
							bpIndex++;
							if (nailObject == null) continue;
							Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
							if (targetBone == null) continue;
							Vector3? desired = null;
							if (corrections != null && corrections.TryGetValue(nailObject, out var c))
							{
								nailObject.position = c.position;
								nailObject.rotation = c.rotation;
								desired = c.desiredLossyScale;
							}
							nailObject.SetParent(targetBone, true);
							if (desired.HasValue) EnforceLossyScale(nailObject, desired.Value);
							}
						bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
						foreach (Transform? nailObject in rightFootNailObjects)
						{
							bpIndex++;
							if (nailObject == null) continue;
							Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
							if (targetBone == null) continue;
							Vector3? desired = null;
							if (corrections != null && corrections.TryGetValue(nailObject, out var c))
							{
								nailObject.position = c.position;
								nailObject.rotation = c.rotation;
								desired = c.desiredLossyScale;
							}
							nailObject.SetParent(targetBone, true);
							if (desired.HasValue) EnforceLossyScale(nailObject, desired.Value);
							}
					}
					else
					{
						foreach (Transform? nailObject in leftFootNailObjects)
							if (nailObject != null) Object.DestroyImmediate(nailObject.gameObject);
						foreach (Transform? nailObject in rightFootNailObjects)
							if (nailObject != null) Object.DestroyImmediate(nailObject.gameObject);
					}
					AssetDatabase.SaveAssets();

					// バリアントの構築
					List<(string Name, Transform?[] VariantNails, string? LeftName, string? RightName)> handVariants = new();
					List<(string Name, Transform?[] VariantNails, string? LeftName, string? RightName)> footVariants = new();
					List<GameObject> objectsToDestroy = new();

					AvatarBlendShapeVariant[]? activeVariants = this.AvatarVariationData.BlendShapeVariants ?? this.AvatarEntity?.BlendShapeVariants;
					ToolConsole.Log($"  BakeBS variants: count={activeVariants?.Length ?? 0}, names=[{(activeVariants != null ? string.Join(", ", activeVariants.Select(v => v.Name)) : "")}]");
					if (activeVariants != null)
					{
						foreach (AvatarBlendShapeVariant variant in activeVariants)
						{
							GameObject? variantPrefabAsset = null;
							string? variantPath = null;
							NailPrefabNodeData[]? scaledVariantNodes = CloneVariantNodes(variant.NailNodes);
							if (scaledVariantNodes != null && scaledVariantNodes.Length > 0)
							{
								variantPrefabAsset = world.anlabo.mdnailtool.Editor.NailDesigns.NailPrefabBuilder.BuildFromNodes(scaledVariantNodes, variant.Name);
								if (variantPrefabAsset != null) objectsToDestroy.Add(variantPrefabAsset);
								ToolConsole.Log($"    variant='{variant.Name}' nailNodes -> in-memory build");
							}
							if (variantPrefabAsset == null)
							{
								variantPath = ResolveVariantPath(variant);
							ToolConsole.Log($"    variant='{variant.Name}' GUID={variant.NailPrefabGUID} path={variantPath ?? "(null)"}");
							if (string.IsNullOrEmpty(variantPath))
							{
								if (!string.IsNullOrEmpty(variant.NailPrefabGUID))
								{
									string msg = string.Format(LanguageManager.S("warn.variant_path_not_found") ?? "Variant '{0}': path not found for GUID={1}", variant.Name, variant.NailPrefabGUID);
									msg += BuildDiagnosticInfo(includeFolder: true);
									ToolConsole.Warn("NailSetup", msg);
									this.Warnings.Add(msg);
								}
								continue;
							}
							// ResolveVariantPath 内で既に load 確認済みのため初回 ImportAsset は省略.
							variantPrefabAsset = NailSetupUtil.LoadPrefabAtPath(variantPath);
							if (variantPrefabAsset == null)
							{
								AssetDatabase.ImportAsset(variantPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
								variantPrefabAsset = NailSetupUtil.LoadPrefabAtPath(variantPath);
							}
							}
							if (variantPrefabAsset == null)
							{
								// 認識失敗時の詳細診断 (File.Exists / meta / GUID 整合)
								bool fileExists = File.Exists(Path.GetFullPath(variantPath));
								bool metaExists = File.Exists(Path.GetFullPath(variantPath + ".meta"));
								string guidFromAdb = AssetDatabase.AssetPathToGUID(variantPath);
								string pathFromAdb = MDNailToolAssetLoader.ResolveGuidToPath(variant.NailPrefabGUID) ?? "";
								string diag = $"[NailDiag] variant='{variant.Name}' GUID={variant.NailPrefabGUID} path={variantPath} fileExists={fileExists} metaExists={metaExists} AssetPathToGUID(path)={guidFromAdb} GUIDToAssetPath(guid)={pathFromAdb} guidMatch={string.Equals(guidFromAdb, variant.NailPrefabGUID, StringComparison.OrdinalIgnoreCase)}";
								ToolConsole.Log(diag);

								string msg = string.Format(LanguageManager.S("warn.variant_prefab_load_failed") ?? "Variant '{0}': failed to load prefab (path={1})", variant.Name, variantPath);
								msg += BuildDiagnosticInfo(includeFolder: true);
								ToolConsole.Warn("NailSetup", msg);
								this.Warnings.Add(msg);
								continue;
							}

							GameObject resolvedVariantPrefab = ResolveShapePrefab(variantPrefabAsset, this.NailShapeName, scaledVariantNodes);
							if (!ReferenceEquals(resolvedVariantPrefab, variantPrefabAsset) && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(resolvedVariantPrefab)))
								objectsToDestroy.Add(resolvedVariantPrefab);
							GameObject instVariant = Object.Instantiate(resolvedVariantPrefab, this.Avatar.transform);
							// C-1 fix: instVariant の objectsToDestroy 追加は全 vNail.SetParent 完了後に移動 (詳細は variants ループ末尾参照).

							// バリアントプレハブの子名からシェイプ接頭辞([Oval]等)を除去
							{
								Regex varPrefixRegex = new(@"(?<prefix>\[.+\]).+");
								Match varPrefixMatch = varPrefixRegex.Match(resolvedVariantPrefab.name);
								if (varPrefixMatch.Success)
								{
									string varPrefix = varPrefixMatch.Groups["prefix"].Value;
									foreach (Transform child in instVariant.transform)
										child.name = child.name.Replace(varPrefix, "");
								}
							}

							Transform?[] varHands = GetHandsNailObjectList(instVariant);
							Transform?[] varLeftFoot = GetLeftFootNailObjectList(instVariant);
							Transform?[] varRightFoot = GetRightFootNailObjectList(instVariant);
							ToolConsole.Log($"    variant='{variant.Name}' instVariant='{instVariant.name}' hands={varHands.Count(t => t != null)}/10 leftFoot={varLeftFoot.Count(t => t != null)}/5 rightFoot={varRightFoot.Count(t => t != null)}/5");

							// バリアントネイルのメッシュが未解決(null)の場合、ベースネイルのメッシュをコピー (FBXが存在しない場合の対策)
							CopyMeshIfNull(varHands, handsNailObjects);
							CopyMeshIfNull(varLeftFoot, leftFootNailObjects);
							CopyMeshIfNull(varRightFoot, rightFootNailObjects);

							// Armatureスケール補正をバリアントネイルにも適用
							Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>? variantCorrections = null;
							if (corrections != null)
							{
								var varAllNails = new List<Transform?>();
								var varAllBoneIndices = new List<int>();
								int vci = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
								foreach (Transform? vn in varHands)
								{
									vci++;
									varAllNails.Add(vn);
									varAllBoneIndices.Add(vci);
								}
								if (this.UseFootNail)
								{
									vci = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
									foreach (Transform? vn in varLeftFoot)
									{
										vci++;
										varAllNails.Add(vn);
										varAllBoneIndices.Add(vci);
									}
									vci = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
									foreach (Transform? vn in varRightFoot)
									{
										vci++;
										varAllNails.Add(vn);
										varAllBoneIndices.Add(vci);
									}
								}
								variantCorrections = ComputeScaleCompensatedTransforms(
									this.Avatar, targetBoneDictionary,
									varAllNails.ToArray(), varAllBoneIndices.ToArray());
							}

							// バリアントネイルも実際の指ボーンの子にして、ローカル座標差分を計算できるようにする
							bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
							foreach (Transform? vNail in varHands)
							{
								bpIndex++;
								if (vNail == null) continue;
								objectsToDestroy.Add(vNail.gameObject);
								Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
								if (targetBone == null) continue;
								Vector3? desired = null;
								if (variantCorrections != null && variantCorrections.TryGetValue(vNail, out var vc))
								{
									vNail.position = vc.position;
									vNail.rotation = vc.rotation;
									desired = vc.desiredLossyScale;
								}
								vNail.SetParent(targetBone, true);
								if (desired.HasValue) EnforceLossyScale(vNail, desired.Value);
									}
							if (varHands.Any(t => t != null))
								handVariants.Add((variant.Name, varHands, variant.LeftBlendShapeName, variant.RightBlendShapeName));

							if (this.UseFootNail)
							{
								bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
								foreach (Transform? vNail in varLeftFoot)
								{
									bpIndex++;
									if (vNail == null) continue;
									objectsToDestroy.Add(vNail.gameObject);
									Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
									if (targetBone == null) continue;
									Vector3? desired = null;
									if (variantCorrections != null && variantCorrections.TryGetValue(vNail, out var vc))
									{
										vNail.position = vc.position;
										vNail.rotation = vc.rotation;
										desired = vc.desiredLossyScale;
									}
									vNail.SetParent(targetBone, true);
									if (desired.HasValue) EnforceLossyScale(vNail, desired.Value);
											}
								bpIndex = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
								foreach (Transform? vNail in varRightFoot)
								{
									bpIndex++;
									if (vNail == null) continue;
									objectsToDestroy.Add(vNail.gameObject);
									Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[bpIndex]];
									if (targetBone == null) continue;
									Vector3? desired = null;
									if (variantCorrections != null && variantCorrections.TryGetValue(vNail, out var vc))
									{
										vNail.position = vc.position;
										vNail.rotation = vc.rotation;
										desired = vc.desiredLossyScale;
									}
									vNail.SetParent(targetBone, true);
									if (desired.HasValue) EnforceLossyScale(vNail, desired.Value);
											}
								var varFeetAll = varLeftFoot.Concat(varRightFoot).ToArray();
								if (varFeetAll.Any(t => t != null))
									footVariants.Add((variant.Name, varFeetAll, variant.LeftBlendShapeName, variant.RightBlendShapeName));
							}

							// 全 vNail が SetParent 完了したのでルート GO を Destroy 対象に追加 (UseFootNail=false でも実行).
							objectsToDestroy.Add(instVariant);
						}
					}

					// 追加オブジェクトの退避 (BakeAndCombineがネイルオブジェクトを破棄するため)
					var preservedAdditionalObjects = new List<(Transform additionalObj, Transform targetBone)>();
					foreach (Transform? nailObject in handsNailObjects)
					{
						if (nailObject == null) continue;
						Transform? targetBone = nailObject.parent;
						if (targetBone == null) continue;
						foreach (Transform child in nailObject.Cast<Transform>().ToArray())
						{
							child.SetParent(null, true);
							preservedAdditionalObjects.Add((child, targetBone));
						}
					}

					// 結合後の爪へ、最寄りの体表面三角形から補間したウェイトを転送する。
					// 手足それぞれで対象ボーンを最も多く共有するSMRを転送元として選ぶ。
					SkinnedMeshRenderer? ResolveWeightSource(IEnumerable<Transform?> nailObjects, bool isHand)
					{
						var targetBones = nailObjects
							.Where(t => t != null && t.parent != null)
							.Select(t => t!.parent)
							.ToHashSet();
						return this.Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
							.Where(smr => smr.sharedMesh != null
								&& smr.bones.Length > 0
								&& smr.sharedMesh.boneWeights.Length == smr.sharedMesh.vertexCount)
							.Select(smr => new { Smr = smr, MatchingBones = smr.bones.Count(targetBones.Contains) })
							.Where(x => x.MatchingBones > 0)
							.OrderByDescending(x => x.MatchingBones)
							.ThenByDescending(x => isHand
								? x.Smr.name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0
								: x.Smr.name.IndexOf("foot", StringComparison.OrdinalIgnoreCase) >= 0)
							.ThenByDescending(x => x.Smr.sharedMesh!.vertexCount)
							.Select(x => x.Smr)
							.FirstOrDefault();
					}
					int weightTransferMode = this.AvatarVariationData.WeightTransferMode;
					SkinnedMeshRenderer? handWeightSource = weightTransferMode == 1 || weightTransferMode == 2
						? ResolveWeightSource(handsNailObjects, true)
						: null;
					SkinnedMeshRenderer? footWeightSource = weightTransferMode == 1
						? ResolveWeightSource(leftFootNailObjects.Concat(rightFootNailObjects), false)
						: null;
					bool[]? handWeightTransferMask = weightTransferMode == 2
						? handsNailObjects.Select((_, i) => i % 5 == 0).ToArray()
						: null;
					ToolConsole.Log($"  Body weight transfer: mode={weightTransferMode} handSource={(handWeightSource == null ? "(none)" : handWeightSource.name)} footSource={(footWeightSource == null ? "(none)" : footWeightSource.name)}");
					// メッシュ統合
					ToolConsole.Log($"  BakeBS: handVariants.Count={handVariants.Count} footVariants.Count={footVariants.Count}");
					bool[] handsIsLeft = handsNailObjects.Select((_, i) => i < 5).ToArray();
					// Shrink_*BS抽出: アバター本体Shrink_* (nailPrefabGUID 空) を Hand/Foot に振り分け
					var handShrinkBS = new List<(string BSName, bool[] NailMask)>();
					var footShrinkBS = new List<(string BSName, bool[] NailMask)>();
					AvatarShrinkBlendShapeVariant[]? explicitShrinkVariants =
						this.AvatarVariationData.ShrinkBlendShapeVariants ?? this.AvatarEntity?.ShrinkBlendShapeVariants;
					if (GlobalSetting.AutoLinkShrinkBS && activeVariants != null)
					{
						foreach (var v in activeVariants)
						{
							if (!string.IsNullOrEmpty(v.NailPrefabGUID)) continue;
							if (string.IsNullOrEmpty(v.Name) || !v.Name.StartsWith("Shrink_", StringComparison.OrdinalIgnoreCase)) continue;
							if (explicitShrinkVariants != null && explicitShrinkVariants.Any(x =>
								string.Equals(x.BlendShapeName, v.Name, StringComparison.OrdinalIgnoreCase))) continue;

							string lower = v.Name.ToLowerInvariant();
							bool isHand = lower.Contains("hand") || lower.Contains("finger") || lower.Contains("glove");
							bool isFoot = lower.Contains("foot") || lower.Contains("toe") || lower.Contains("lower_leg") || lower.Contains("socks") || lower.Contains("stocking");
							if (!isHand && !isFoot) continue;

							bool leftOnly = v.Name.EndsWith("_L") || v.Name.EndsWith(".L");
							bool rightOnly = v.Name.EndsWith("_R") || v.Name.EndsWith(".R");
							if (isHand) handShrinkBS.Add((v.Name, BuildLegacySideMask(handsNailObjects.Length, leftOnly, rightOnly)));
							if (isFoot) footShrinkBS.Add((v.Name, BuildLegacySideMask(leftFootNailObjects.Length + rightFootNailObjects.Length, leftOnly, rightOnly)));
						}
					}

					if (GlobalSetting.AutoLinkShrinkBS && explicitShrinkVariants != null)
					{
						foreach (AvatarShrinkBlendShapeVariant shrink in explicitShrinkVariants)
						{
							if (string.IsNullOrWhiteSpace(shrink.BlendShapeName) || shrink.Targets == null || shrink.Targets.Length == 0)
							{
								ToolConsole.Log($"[Warning] Shrink BS definition is invalid: '{shrink.BlendShapeName ?? "(null)"}' targets=0");
								continue;
							}
							string blendShapeName = shrink.BlendShapeName!;
							bool[] handMask = BuildShrinkTargetMask(shrink.Targets, true);
							bool[] footMask = BuildShrinkTargetMask(shrink.Targets, false);
							if (handMask.Any(x => x)) handShrinkBS.Add((blendShapeName, handMask));
							if (footMask.Any(x => x)) footShrinkBS.Add((blendShapeName, footMask));
							if (!handMask.Any(x => x) && !footMask.Any(x => x))
								ToolConsole.Log($"[Warning] Shrink BS '{shrink.BlendShapeName}' has no valid targets: [{string.Join(", ", shrink.Targets)}]");
						}
					}
					ToolConsole.Log($"  Shrink BS: hand={handShrinkBS.Count} foot={footShrinkBS.Count}");

					handCombinedGo = NailSetupUtil.BakeAndCombineNailMeshes(
						handsNailObjects, nailPrefabObject, handWrapperName, bsPath,
						handVariants.Count > 0 ? handVariants.ToArray() : null,
						handsIsLeft,
						handWeightSource,
						handShrinkBS.Count > 0 ? handShrinkBS.ToArray() : null,
						handWeightTransferMask);
					ToolConsole.Log($"  BakeBS hand result: {(handCombinedGo == null ? "(null)" : handCombinedGo.name)} BS frames={(handCombinedGo?.GetComponent<SkinnedMeshRenderer>()?.sharedMesh?.blendShapeCount ?? -1)}");

					if (this.UseFootNail)
					{
						bool[] feetIsLeft = leftFootNailObjects.Select(_ => true)
							.Concat(rightFootNailObjects.Select(_ => false)).ToArray();
						footCombinedGo = NailSetupUtil.BakeAndCombineNailMeshes(
							leftFootNailObjects.Concat(rightFootNailObjects).ToArray(),
							nailPrefabObject, footWrapperName, bsPath,
							footVariants.Count > 0 ? footVariants.ToArray() : null,
							feetIsLeft,
							footWeightSource,
							footShrinkBS.Count > 0 ? footShrinkBS.ToArray() : null);
						ToolConsole.Log($"  BakeBS foot result: {(footCombinedGo == null ? "(null)" : footCombinedGo.name)} BS frames={(footCombinedGo?.GetComponent<SkinnedMeshRenderer>()?.sharedMesh?.blendShapeCount ?? -1)}");
					}

					// 退避した追加オブジェクトを統合ラッパーに復元 (BoneProxy付き)
					if (preservedAdditionalObjects.Count > 0 && handCombinedGo != null)
					{
						foreach (var (additionalObj, targetBone) in preservedAdditionalObjects)
						{
							if (additionalObj == null) continue;
							additionalObj.SetParent(handCombinedGo.transform, true);
							ModularAvatarBoneProxy bp = additionalObj.gameObject.AddComponent<ModularAvatarBoneProxy>();
							bp.attachmentMode = BoneProxyAttachmentMode.AsChildKeepWorldPose;
							bp.target = targetBone;
						}
					}

					// バリアントインスタンスの破棄
					foreach (GameObject obj in objectsToDestroy)
					{
						if (obj != null) Object.DestroyImmediate(obj);
					}
				}

				GameObject handWrapper;
				GameObject? footWrapper = null;

				if (this.BakeBlendShapes && handCombinedGo != null)
				{
					handWrapper = handCombinedGo;
				}
				else
				{
					// ---- HandNailラッパー作成 ----
					handWrapper = new GameObject(handWrapperName);
					Undo.RegisterCreatedObjectUndo(handWrapper, "Nail Setup");
					handWrapper.transform.SetParent(nailPrefabObject.transform, false);
					foreach (Transform? nailObject in handsNailObjects)
					{
						if (nailObject == null) continue;
						nailObject.SetParent(handWrapper.transform, false);
					}
				}

				if (this.UseFootNail)
				{
					if (this.BakeBlendShapes && footCombinedGo != null)
					{
						footWrapper = footCombinedGo;
					}
					else
					{
						// ---- FootNailラッパー作成 ----
						footWrapper = new GameObject(footWrapperName);
						Undo.RegisterCreatedObjectUndo(footWrapper, "Nail Setup");
						footWrapper.transform.SetParent(nailPrefabObject.transform, false);
						foreach (Transform? nailObject in leftFootNailObjects)
						{
							if (nailObject == null) continue;
							nailObject.SetParent(footWrapper.transform, false);
						}
						foreach (Transform? nailObject in rightFootNailObjects)
						{
							if (nailObject == null) continue;
							nailObject.SetParent(footWrapper.transform, false);
						}
					}
				}
				else if (!this.BakeBlendShapes)
				{
					foreach (Transform? nailObject in leftFootNailObjects)
						if (nailObject != null) Object.DestroyImmediate(nailObject.gameObject);
					foreach (Transform? nailObject in rightFootNailObjects)
						if (nailObject != null) Object.DestroyImmediate(nailObject.gameObject);
				}

				void AttachNailWithBoneProxyLikeDirect(Transform nailObject, Transform targetBone, Vector3? desired)
				{
					Transform? wrapperParent = nailObject.parent;
					nailObject.SetParent(targetBone, true);
					if (desired.HasValue) EnforceLossyScale(nailObject, desired.Value);

					Vector3 directLocalPosition = nailObject.localPosition;
					Quaternion directLocalRotation = nailObject.localRotation;
					Vector3 directLocalScale = nailObject.localScale;

					GameObject proxyObject = new GameObject($"{nailObject.name}_BoneProxy");
					Undo.RegisterCreatedObjectUndo(proxyObject, "Nail Setup BoneProxy");
					if (wrapperParent != null) proxyObject.transform.SetParent(wrapperParent, false);
					proxyObject.transform.SetPositionAndRotation(targetBone.position, targetBone.rotation);
					proxyObject.transform.localScale = Vector3.one;

					ModularAvatarBoneProxy boneProxy = proxyObject.AddComponent<ModularAvatarBoneProxy>();
					boneProxy.attachmentMode = BoneProxyAttachmentMode.AsChildAtRoot;
					boneProxy.matchScale = true;
					boneProxy.target = targetBone;

					nailObject.SetParent(proxyObject.transform, false);
					nailObject.localPosition = directLocalPosition;
					nailObject.localRotation = directLocalRotation;
					nailObject.localScale = directLocalScale;
				}
				// ---- BoneProxy設定 (BakeBlendShapes=falseの場合のみ) ----
				if (!this.BakeBlendShapes)
				{
					int index = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
					foreach (Transform? nailObject in handsNailObjects)
					{
						index++;
						if (nailObject == null) continue;
						Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
						Vector3? desired = null;
						if (corrections != null && corrections.TryGetValue(nailObject, out var c))
						{
							nailObject.position = c.position;
							nailObject.rotation = c.rotation;
							desired = c.desiredLossyScale;
						}
						if (targetBone == null) continue;
						AttachNailWithBoneProxyLikeDirect(nailObject, targetBone, desired);
					}

					if (this.UseFootNail)
					{
						index = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
						foreach (Transform? nailObject in leftFootNailObjects)
						{
							index++;
							if (nailObject == null) continue;
							Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
							Vector3? desired = null;
							if (corrections != null && corrections.TryGetValue(nailObject, out var c))
							{
								nailObject.position = c.position;
								nailObject.rotation = c.rotation;
								desired = c.desiredLossyScale;
								}
							if (targetBone == null) continue;
						AttachNailWithBoneProxyLikeDirect(nailObject, targetBone, desired);
						}
						index = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
						foreach (Transform? nailObject in rightFootNailObjects)
						{
							index++;
							if (nailObject == null) continue;
							Transform? targetBone = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
							Vector3? desired = null;
							if (corrections != null && corrections.TryGetValue(nailObject, out var c))
							{
								nailObject.position = c.position;
								nailObject.rotation = c.rotation;
								desired = c.desiredLossyScale;
								}
							if (targetBone == null) continue;
						AttachNailWithBoneProxyLikeDirect(nailObject, targetBone, desired);
						}
					}
				}

				// ---- BlendShapeSync設定 (ブレンドシェイプが同期元として設定されている場合) ----
				if (!this.BakeBlendShapes && resolvedSourceSmrs.Count > 0)
				{
					var allNailObjectsForSync = handsNailObjects
						.Concat(this.UseFootNail ? (IEnumerable<Transform?>)leftFootNailObjects.Concat(rightFootNailObjects) : Enumerable.Empty<Transform?>());
					foreach (Transform? nailObject in allNailObjectsForSync)
					{
						if (nailObject == null) continue;
						SkinnedMeshRenderer? nailSmr = nailObject.GetComponent<SkinnedMeshRenderer>();
						if (nailSmr == null || nailSmr.sharedMesh == null) continue;

						var bindings = new List<BlendshapeBinding>();
						foreach ((SkinnedMeshRenderer sourceSmr, string _) in resolvedSourceSmrs)
						{
							Mesh? sourceMesh = sourceSmr.sharedMesh;
							if (sourceMesh == null) continue;
							for (int si = 0; si < sourceMesh.blendShapeCount; si++)
							{
								string shapeName = sourceMesh.GetBlendShapeName(si);
								if (nailSmr.sharedMesh.GetBlendShapeIndex(shapeName) < 0) continue;
								bindings.Add(new BlendshapeBinding
								{
									ReferenceMesh = CreateAvatarRef(sourceSmr.gameObject),
									Blendshape = shapeName,
									LocalBlendshape = shapeName
								});
							}
						}
						if (bindings.Count > 0)
						{
							ModularAvatarBlendshapeSync bsSync = nailObject.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
							bsSync.Bindings = bindings;
						}
					}
				}

				// ---- re-wear / [An-Labo]まとめ処理 ----
				if (this.MergeAnLabo)
				{
					Transform? anLaboParent = this.Avatar.transform.Find("[An-Labo]");
					if (anLaboParent == null)
					{
						GameObject anLaboObj = new GameObject("[An-Labo]");
						anLaboObj.transform.SetParent(this.Avatar.transform, false);
						Undo.RegisterCreatedObjectUndo(anLaboObj, "Nail Setup");
						anLaboParent = anLaboObj.transform;
					}
					Transform? existingNailRoot = anLaboParent.Find(nailPrefabObject.name);
					if (existingNailRoot != null && existingNailRoot.GetComponent<MDNailObjectMarker>() != null)
					{
						// A-1 fix: 既存ルート内の旧 HandNail/FootNail ラッパーを先に破棄してから新wrapperを統合する (色違い再Apply時の多重積み防止).
						Transform? oldHand = existingNailRoot.Find(handWrapperName);
						Transform? oldFoot = existingNailRoot.Find(footWrapperName);
						if (oldHand != null) Undo.DestroyObjectImmediate(oldHand.gameObject);
						if (oldFoot != null) Undo.DestroyObjectImmediate(oldFoot.gameObject);

						handWrapper.transform.SetParent(existingNailRoot, false);
						if (footWrapper != null) footWrapper.transform.SetParent(existingNailRoot, false);
						Object.DestroyImmediate(nailPrefabObject);
						nailPrefabObject = existingNailRoot.gameObject;
					}
					else
					{
						nailPrefabObject.transform.SetParent(anLaboParent, false);
					}
				}
				else
				{
					Transform? existingRoot = this.Avatar.transform.Find(nailPrefabObject.name);
					if (existingRoot != null && existingRoot.GetComponent<MDNailObjectMarker>() != null)
					{
						// A-1 fix: MergeAnLabo=false ルートでも同様に旧wrapperを破棄してから統合する.
						Transform? oldHand = existingRoot.Find(handWrapperName);
						Transform? oldFoot = existingRoot.Find(footWrapperName);
						if (oldHand != null) Undo.DestroyObjectImmediate(oldHand.gameObject);
						if (oldFoot != null) Undo.DestroyObjectImmediate(oldFoot.gameObject);

						handWrapper.transform.SetParent(existingRoot, false);
						if (footWrapper != null) footWrapper.transform.SetParent(existingRoot, false);
						Object.DestroyImmediate(nailPrefabObject);
						nailPrefabObject = existingRoot.gameObject;
					}
				}

				// B-1 fix: AddComponent の結果を Undo に登録 (Undo時にMarkerだけ消えてGOが残るデグレを防ぐ).
				if (nailPrefabObject.GetComponent<MDNailObjectMarker>() == null)
				{
					MDNailObjectMarker marker = nailPrefabObject.AddComponent<MDNailObjectMarker>();
					Undo.RegisterCreatedObjectUndo(marker, "Nail Setup Marker");
				}

				// ---- MA Mesh Settings ----
				// ネイル階層だけに Hips 基準の安全 Bounds を上書きする。
				// アバター本体の Renderer / Bounds は変更しない。
				ModularAvatarMeshSettings meshSettings = nailPrefabObject.GetComponent<ModularAvatarMeshSettings>();
				if (meshSettings == null)
				{
					meshSettings = nailPrefabObject.AddComponent<ModularAvatarMeshSettings>();
				}
				meshSettings.InheritProbeAnchor = ModularAvatarMeshSettings.InheritMode.SetOrInherit;
				meshSettings.InheritBounds = ModularAvatarMeshSettings.InheritMode.Set;

				Animator? animator = this.Avatar.GetComponent<Animator>();
				if (animator != null)
				{
					Transform? chest = animator.GetBoneTransform(HumanBodyBones.Chest);
					if (chest != null)
						meshSettings.ProbeAnchor = CreateAvatarRef(chest.gameObject);
				}

				Transform boundsRoot = animator != null && animator.isHuman
					? animator.GetBoneTransform(HumanBodyBones.Hips) ?? this.Avatar.transform
					: this.Avatar.transform;
				meshSettings.RootBone = CreateAvatarRef(boundsRoot.gameObject);
				Matrix4x4 avatarToBoundsRoot = boundsRoot.worldToLocalMatrix * this.Avatar.transform.localToWorldMatrix;
				Bounds maBounds = TransformBoundsForMeshSettings(
					new Bounds(new Vector3(0f, 0.75f, 0f), Vector3.one * 2.5f),
					avatarToBoundsRoot);
				foreach (SkinnedMeshRenderer nailSmr in nailPrefabObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
				{
					if (nailSmr.sharedMesh == null) continue;
					Matrix4x4 meshToBoundsRoot = boundsRoot.worldToLocalMatrix * nailSmr.transform.localToWorldMatrix;
					Bounds shapeBounds = TransformBoundsForMeshSettings(nailSmr.sharedMesh.bounds, meshToBoundsRoot);
					maBounds.Encapsulate(shapeBounds.min);
					maBounds.Encapsulate(shapeBounds.max);
				}
				meshSettings.Bounds = maBounds;

				// avatar-levelブレンドシェイプバリアントのBlendShapeSync設定 (nailPrefabObjectがアバター階層下に配置された後に実行する)
				AvatarBlendShapeVariant[]? syncVariants = this.AvatarVariationData.BlendShapeVariants ?? this.AvatarEntity?.BlendShapeVariants;
				AvatarShrinkBlendShapeVariant[]? shrinkSyncVariants = this.AvatarVariationData.ShrinkBlendShapeVariants ?? this.AvatarEntity?.ShrinkBlendShapeVariants;
				if (syncVariants != null || shrinkSyncVariants != null)
				{
					// BakeAndCombineで生成されたCombined SMRを含む全子オブジェクトを対象
					foreach (Transform child in nailPrefabObject.transform)
					{
						SkinnedMeshRenderer? bsSmr = child.GetComponent<SkinnedMeshRenderer>();
						if (bsSmr == null || bsSmr.sharedMesh == null) continue;
						if (bsSmr.sharedMesh.blendShapeCount == 0) continue;
						var variantBindings = new List<BlendshapeBinding>();
						foreach (AvatarBlendShapeVariant variant in syncVariants ?? Array.Empty<AvatarBlendShapeVariant>())
						{
							if (string.IsNullOrEmpty(variant.SyncSourceSmrName)) continue;
							Transform? srcSmrTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
								.FirstOrDefault(t => t.name == variant.SyncSourceSmrName);
							if (srcSmrTransform == null) {
								srcSmrTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
									.FirstOrDefault(t => string.Equals(t.name, variant.SyncSourceSmrName, System.StringComparison.OrdinalIgnoreCase));
							}
							if (srcSmrTransform == null) {
								srcSmrTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
									.FirstOrDefault(t => t.GetComponent<SkinnedMeshRenderer>() != null
										&& (t.name.Contains(variant.SyncSourceSmrName!) || variant.SyncSourceSmrName!.Contains(t.name)));
							}
							if (srcSmrTransform == null) {
								SkinnedMeshRenderer? visemeSmr = this.Avatar.VisemeSkinnedMesh;
								srcSmrTransform = this.Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
									.Where(smr => smr != visemeSmr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
									.OrderByDescending(smr => smr.sharedMesh!.blendShapeCount)
									.FirstOrDefault()?.transform;
								if (srcSmrTransform != null) {
									ToolConsole.Log(string.Format(LanguageManager.S("info.blendshape_sync_using_fallback") ?? "BlendShapeSync: '{0}' not found, using fallback '{1}'", variant.SyncSourceSmrName, srcSmrTransform.name));

								}
							}
							if (srcSmrTransform == null) { ToolConsole.Warn("NailSetup", string.Format(LanguageManager.S("warn.blendshape_sync_source_missing") ?? "BlendShape sync source mesh '{0}' not found on avatar", variant.SyncSourceSmrName) + BuildDiagnosticInfo()); continue; }

							if (!string.IsNullOrEmpty(variant.LeftBlendShapeName) || !string.IsNullOrEmpty(variant.RightBlendShapeName))
							{
								// L/R分割モード: 結合メッシュ上のL/R Blendshapeをそれぞれアバターのものとバインド
								AvatarObjectReference aoRef = CreateAvatarRef(srcSmrTransform.gameObject);
								foreach (string? lrNameOrNull in new[] { variant.Name, variant.LeftBlendShapeName, variant.RightBlendShapeName })
								{
									if (string.IsNullOrEmpty(lrNameOrNull)) continue;
									string lrName = lrNameOrNull!;
									string actualLRName = lrName;
									string normalizedLRName = lrName.Replace(" ", "").Replace("　", "");
									for (int shi = 0; shi < bsSmr.sharedMesh.blendShapeCount; shi++)
									{
										string sn = bsSmr.sharedMesh.GetBlendShapeName(shi);
										if (sn.Replace(" ", "").Replace("　", "") == normalizedLRName)
										{
											actualLRName = sn;
											break;
										}
									}
									if (bsSmr.sharedMesh.GetBlendShapeIndex(actualLRName) < 0) continue;
									variantBindings.Add(new BlendshapeBinding
									{
										ReferenceMesh = aoRef,
										Blendshape = actualLRName,
										LocalBlendshape = actualLRName
									});
								}
							}
							else
							{
								// 既存動作(L/R未設定時)
								string actualShapeName = variant.Name;
								string normalizedVariantName = variant.Name.Replace(" ", "").Replace("　", "");
								for (int shi = 0; shi < bsSmr.sharedMesh.blendShapeCount; shi++)
								{
									string sn = bsSmr.sharedMesh.GetBlendShapeName(shi);
									if (sn.Replace(" ", "").Replace("　", "") == normalizedVariantName)
									{
										actualShapeName = sn;
										break;
									}
								}

								int bsIndex = bsSmr.sharedMesh.GetBlendShapeIndex(actualShapeName);
								if (bsIndex < 0) continue;

								AvatarObjectReference aoRef = CreateAvatarRef(srcSmrTransform.gameObject);
								variantBindings.Add(new BlendshapeBinding
								{
									ReferenceMesh = aoRef,
									Blendshape = actualShapeName,
									LocalBlendshape = actualShapeName
								});
							}
							}
						foreach (AvatarShrinkBlendShapeVariant shrink in shrinkSyncVariants ?? Array.Empty<AvatarShrinkBlendShapeVariant>())
						{
							if (string.IsNullOrEmpty(shrink.SyncSourceSmrName)
								|| string.IsNullOrEmpty(shrink.BlendShapeName)
								|| bsSmr.sharedMesh.GetBlendShapeIndex(shrink.BlendShapeName) < 0) continue;
							Transform? source = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
								.FirstOrDefault(t => string.Equals(t.name, shrink.SyncSourceSmrName, StringComparison.OrdinalIgnoreCase));
							if (source == null || source.GetComponent<SkinnedMeshRenderer>() == null)
							{
								ToolConsole.Log($"[Warning] Shrink BS sync source '{shrink.SyncSourceSmrName}' not found for '{shrink.BlendShapeName}'");
								continue;
							}
							variantBindings.Add(new BlendshapeBinding
							{
								ReferenceMesh = CreateAvatarRef(source.gameObject),
								Blendshape = shrink.BlendShapeName,
								LocalBlendshape = shrink.BlendShapeName
							});
						}
						if (variantBindings.Count > 0)
						{
							ModularAvatarBlendshapeSync variantBsSync = child.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
							variantBsSync.Bindings = variantBindings;

						}
					}
				}

				if (this.GenerateExpressionMenu)
					this.SetupExpressionMenu(nailPrefabObject);
#else
				Undo.RevertAllInCurrentGroup();
				throw new NailToolUserException("NailSetup", "The setup for ModularAvatar cannot be executed in environments where ModularAvatar is not installed.");
#endif
		}

#if MD_NAIL_FOR_MA
		private static Bounds TransformBoundsForMeshSettings(Bounds source, Matrix4x4 matrix) {
			Vector3 min = source.min;
			Vector3 max = source.max;
			Bounds transformed = new Bounds(matrix.MultiplyPoint3x4(min), Vector3.zero);
			for (int x = 0; x < 2; x++)
			for (int y = 0; y < 2; y++)
			for (int z = 0; z < 2; z++) {
				Vector3 corner = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
				transformed.Encapsulate(matrix.MultiplyPoint3x4(corner));
			}
			return transformed;
		}

		private static bool[] BuildLegacySideMask(int count, bool leftOnly, bool rightOnly) {
			bool[] mask = new bool[count];
			int leftCount = count / 2;
			for (int i = 0; i < count; i++) {
				bool isLeft = i < leftCount;
				mask[i] = leftOnly ? isLeft : rightOnly ? !isLeft : true;
			}
			return mask;
		}

		private static bool[] BuildShrinkTargetMask(IEnumerable<string> targets, bool hand) {
			string prefix = hand ? "Hand" : "Foot";
			string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
			string[] nailKeys = Enumerable.Range(0, 10)
				.Select(i => $"{prefix}{(i < 5 ? "L" : "R")}.{fingers[i % 5]}")
				.ToArray();
			bool[] mask = new bool[nailKeys.Length];
			foreach (string rawTarget in targets.Where(x => !string.IsNullOrWhiteSpace(x))) {
				string target = rawTarget.Trim();
				for (int i = 0; i < nailKeys.Length; i++) {
					string key = nailKeys[i];
					bool exact = string.Equals(target, key, StringComparison.OrdinalIgnoreCase);
					bool group = (string.Equals(target, prefix, StringComparison.OrdinalIgnoreCase)
						|| string.Equals(target, prefix + "L", StringComparison.OrdinalIgnoreCase) && key.StartsWith(prefix + "L.", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(target, prefix + "R", StringComparison.OrdinalIgnoreCase) && key.StartsWith(prefix + "R.", StringComparison.OrdinalIgnoreCase));
					if (exact || group) mask[i] = true;
				}
			}
			return mask;
		}

		private static AvatarObjectReference CreateAvatarRef(GameObject obj) {
			// MAバージョン非依存. Set(GameObject)は全バージョンで存在し例外を投げない
			var r = new AvatarObjectReference();
			r.Set(obj);
			return r;
		}

		private static void SetMenuSubMenu(ModularAvatarMenuItem item) {
#if MA_HAS_PORTABLE_API
			item.PortableControl.Type = PortableControlType.SubMenu;
#else
			if (item.Control == null) item.Control = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control();
			item.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.SubMenu;
#endif
		}

		private static void SetMenuToggle(ModularAvatarMenuItem item, float value = 1f) {
#if MA_HAS_PORTABLE_API
			item.PortableControl.Type = PortableControlType.Toggle;
			item.PortableControl.Value = value;
#else
			if (item.Control == null) item.Control = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control();
			item.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.Toggle;
			item.Control.value = value;
#endif
		}

		private static void SetMenuIcon(ModularAvatarMenuItem item, Texture2D? icon) {
#if MA_HAS_PORTABLE_API
			item.PortableControl.Icon = icon;
#else
			if (item.Control == null) item.Control = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control();
			item.Control.icon = icon;
#endif
		}

		private void SetupExpressionMenu(GameObject nailRoot) {
			var firstEntry = this.NailDesignAndVariationNames.FirstOrDefault(t => t.Item1 != null);
			string designName = firstEntry.Item1 != null
				? firstEntry.Item1.DesignName : (this.OverrideMaterial?.name ?? "Nail");
			string colorName = firstEntry.Item1 != null ? firstEntry.Item3 : "";
			string menuLabel = string.IsNullOrEmpty(colorName) ? designName : $"{designName}_{colorName}";
			string variationName = this.AvatarVariationData.VariationName;
			string handWrapperName = $"HandNail_{variationName}";
			string footWrapperName = $"FootNail_{variationName}";

			// サムネイル取得
			Texture2D? thumbnail = null;
			{
				using DBNailDesign db = new();
				NailDesign? design = db.FindNailDesignByDesignName(designName);
				if (design != null) {
					thumbnail = MDNailToolAssetLoader.LoadThumbnail(design.ThumbnailGUID, design.DesignName);
				}
			}

			// ---- MergeAnLabo: [An-Labo]親にMenuInstaller+SubMenuItem (まだ無ければ) ----
			if (this.MergeAnLabo) {
				GameObject anLaboObj = nailRoot.transform.parent?.gameObject ?? this.Avatar.gameObject;
				if (anLaboObj.GetComponent<ModularAvatarMenuInstaller>() == null) {
					// [An-Labo]新規作成時: 最初のネイルのサムネイルを設定
					anLaboObj.AddComponent<ModularAvatarMenuInstaller>();
					var anLaboMenuItem = anLaboObj.AddComponent<ModularAvatarMenuItem>();
					SetMenuSubMenu(anLaboMenuItem);
					SetMenuIcon(anLaboMenuItem, thumbnail);
					anLaboMenuItem.label = "An-Labo";
					anLaboMenuItem.MenuSource = SubmenuSource.Children;
				} else {
					// [An-Labo]既存 (2本目以降のネイル追加時): アイコンをnullに更新
					var existingMenuItem = anLaboObj.GetComponent<ModularAvatarMenuItem>();
					if (existingMenuItem != null)
						SetMenuIcon(existingMenuItem, null);
				}
			} else {
				// MergeAnLabo=false: nailRoot自身にMenuInstaller
				if (nailRoot.GetComponent<ModularAvatarMenuInstaller>() == null)
					nailRoot.AddComponent<ModularAvatarMenuInstaller>();
			}

			// ---- nailRoot に MenuItem ----
			ModularAvatarMenuItem rootMenuItem = nailRoot.AddComponent<ModularAvatarMenuItem>();
			SetMenuIcon(rootMenuItem, thumbnail);
			rootMenuItem.label = menuLabel;
			if (this.SplitHandFoot) {
				SetMenuSubMenu(rootMenuItem);
				rootMenuItem.MenuSource = SubmenuSource.Children;
			} else {
				// SplitHandFoot=OFF: nailRoot自体にObjectToggle (HandNail/FootNailラッパーをまとめてON/OFF)
				SetMenuToggle(rootMenuItem, 1);
				rootMenuItem.isSaved = true;
				rootMenuItem.isSynced = true;
				rootMenuItem.isDefault = true;
				rootMenuItem.automaticValue = true;

				ModularAvatarObjectToggle rootToggle = nailRoot.AddComponent<ModularAvatarObjectToggle>();
				// メニューの ON を「着用中」とし、OFF 時だけネイルを非表示にする。
				rootToggle.Inverted = true;
				var toggleTargets = new System.Collections.Generic.List<ToggledObject>();
				// HandNailラッパー内の各ネイルオブジェクトを個別に登録 (BakeBS で子無し時は wrapper 自身)
				Transform? hw = nailRoot.transform.Find(handWrapperName);
				if (hw != null) {
					int handAdded = 0;
					foreach (Transform child in hw) {
						toggleTargets.Add(new ToggledObject { Object = CreateAvatarRef(child.gameObject), Active = false });
						handAdded++;
					}
					if (handAdded == 0) {
						toggleTargets.Add(new ToggledObject { Object = CreateAvatarRef(hw.gameObject), Active = false });
					}
				}
				// FootNailラッパー内の各ネイルオブジェクトを個別に登録 (BakeBS で子無し時は wrapper 自身)
				if (this.UseFootNail) {
					Transform? fw = nailRoot.transform.Find(footWrapperName);
					if (fw != null) {
						int footAdded = 0;
						foreach (Transform child in fw) {
							toggleTargets.Add(new ToggledObject { Object = CreateAvatarRef(child.gameObject), Active = false });
							footAdded++;
						}
						if (footAdded == 0) {
							toggleTargets.Add(new ToggledObject { Object = CreateAvatarRef(fw.gameObject), Active = false });
						}
					}
				}
				rootToggle.Objects = toggleTargets;
			}

			// ---- SplitHandFoot=ON: HandNail/FootNailにObjectToggle+MenuItem (アイコンなし) ----
			if (this.SplitHandFoot) {
				Transform? handWrapperT = nailRoot.transform.Find(handWrapperName);
				if (handWrapperT != null) {
					// BakeBS では wrapper 自身が統合済み SMR になり、子を持たない。
					// MenuItem/ObjectToggle を wrapper 自身に付けて wrapper を対象にすると自己参照になり、
					// MA の初期 Active 判定が循環するため、メニュー制御用 GO を兄弟として分離する。
					GameObject handControl = new($"HandNailToggle_{variationName}");
					handControl.transform.SetParent(nailRoot.transform, false);
					handControl.hideFlags = HideFlags.HideInHierarchy;
					ModularAvatarObjectToggle handToggle = handControl.AddComponent<ModularAvatarObjectToggle>();
					// メニューの ON を「着用中」として見せる。ネイル本体は初期状態で有効なため、
					// 反転条件にすると自動生成パラメータの初期値が ON になり、OFF 時だけ非表示になる。
					handToggle.Inverted = true;
					// BakeBS で wrapper 自身が SMR 統合 GO になり子なしのケース対応: 子があれば子を、無ければ wrapper 自身を target に.
					List<ToggledObject> handToggleObjects = handWrapperT.Cast<Transform>()
						.Select(t => new ToggledObject {
							Object = CreateAvatarRef(t.gameObject),
							Active = false
						})
						.ToList();
					if (handToggleObjects.Count == 0) {
						handToggleObjects.Add(new ToggledObject { Object = CreateAvatarRef(handWrapperT.gameObject), Active = false });
					}
					handToggle.Objects = handToggleObjects;

					ModularAvatarMenuItem handMenuItem = handControl.AddComponent<ModularAvatarMenuItem>();
					SetMenuToggle(handMenuItem, 1);
					SetMenuIcon(handMenuItem, null);
					handMenuItem.isSaved = true;
					handMenuItem.isSynced = true;
					handMenuItem.isDefault = true;
					handMenuItem.automaticValue = true;
					handMenuItem.label = $"HandNail - {variationName}";
				}

				if (this.UseFootNail) {
					Transform? footWrapperT = nailRoot.transform.Find(footWrapperName);
					if (footWrapperT != null) {
						GameObject footControl = new($"FootNailToggle_{variationName}");
						footControl.transform.SetParent(nailRoot.transform, false);
						footControl.hideFlags = HideFlags.HideInHierarchy;
						ModularAvatarObjectToggle footToggle = footControl.AddComponent<ModularAvatarObjectToggle>();
						footToggle.Inverted = true;
						List<ToggledObject> footToggleObjects = footWrapperT.Cast<Transform>()
							.Select(t => new ToggledObject {
								Object = CreateAvatarRef(t.gameObject),
								Active = false
							})
							.ToList();
						if (footToggleObjects.Count == 0) {
							footToggleObjects.Add(new ToggledObject { Object = CreateAvatarRef(footWrapperT.gameObject), Active = false });
						}
						footToggle.Objects = footToggleObjects;

						ModularAvatarMenuItem footMenuItem = footControl.AddComponent<ModularAvatarMenuItem>();
						SetMenuToggle(footMenuItem, 1);
						SetMenuIcon(footMenuItem, null);
						footMenuItem.isSaved = true;
						footMenuItem.isSynced = true;
						footMenuItem.isDefault = true;
						footMenuItem.automaticValue = true;
						footMenuItem.label = $"FootNail - {variationName}";
					}
				}
			}
		}

		private static string SanitizeAssetPathSegment(string value) {
			string sanitized = Regex.Replace(value, @"[\\/:*?""<>|]", "_").Trim();
			return string.IsNullOrEmpty(sanitized) ? "Avatar" : sanitized;
		}
#endif
	}
}

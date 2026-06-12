using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Language;
using world.anlabo.mdnailtool.Editor.Model;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public partial class NailSetupProcessor {
		// AvatarVariationData.BlendShapeSyncSources を解決し SMR とパスのペアを返す. 解決失敗時は body 推測フォールバックを実行する.
		private List<(SkinnedMeshRenderer sourceSmr, string sourcePath)> ResolveBlendShapeSyncSources()
		{
			var resolvedSourceSmrs = new List<(SkinnedMeshRenderer, string)>();
			string[]? blendShapeSyncSources = this.AvatarVariationData.BlendShapeSyncSources;
			if (blendShapeSyncSources == null || blendShapeSyncSources.Length == 0) return resolvedSourceSmrs;

			foreach (string sourcePath in blendShapeSyncSources) {
				Transform? sourceTransform = this.Avatar.transform.Find(sourcePath);
				if (sourceTransform == null) {
					sourceTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
						.FirstOrDefault(t => t.name == System.IO.Path.GetFileName(sourcePath));
				}
				if (sourceTransform == null) {
					string sourceName = System.IO.Path.GetFileName(sourcePath);
					sourceTransform = this.Avatar.transform.GetComponentsInChildren<Transform>(true)
						.FirstOrDefault(t => string.Equals(t.name, sourceName, System.StringComparison.OrdinalIgnoreCase));
				}
				if (sourceTransform == null) {
					ToolConsole.Warn("NailSetup", $"{LanguageManager.S("warn.blendshape_sync_source_not_found") ?? "BlendShape sync source mesh not found"}: '{sourcePath}'{BuildDiagnosticInfo()}");
					continue;
				}
				SkinnedMeshRenderer? sourceSmr = sourceTransform.GetComponent<SkinnedMeshRenderer>();
				if (sourceSmr == null || sourceSmr.sharedMesh == null) {
					ToolConsole.Warn("NailSetup", $"{LanguageManager.S("warn.blendshape_sync_source_no_mesh") ?? "BlendShape sync source has no mesh data"}: '{sourcePath}'{BuildDiagnosticInfo()}");
					continue;
				}
				resolvedSourceSmrs.Add((sourceSmr, sourcePath));
			}

			// フォールバック: 指定された名前で見つからない場合、VisemeSkinnedMesh 以外で BlendShape を持つ SMR から体メッシュを推測する.
			if (resolvedSourceSmrs.Count == 0) {
				SkinnedMeshRenderer? visemeSmr = this.Avatar.VisemeSkinnedMesh;
				var fallbackCandidate = this.Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
					.Where(smr => smr != visemeSmr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
					.OrderByDescending(smr => smr.sharedMesh!.blendShapeCount)
					.FirstOrDefault();
				if (fallbackCandidate != null) {
					resolvedSourceSmrs.Add((fallbackCandidate, fallbackCandidate.gameObject.name));
					ToolConsole.Log(string.Format(LanguageManager.S("info.blendshape_sync_source_fallback") ?? "BlendShapeSyncSource fallback: using '{0}'", fallbackCandidate.gameObject.name));
				}
			}

			return resolvedSourceSmrs;
		}

		// resolvedSourceSmrs があれば BakeBlendShapesToNails を呼ぶ. UseFootNail=false の場合は足ネイルをベイク対象から除外する.
		private void BakeBlendShapesIfNeeded(
			List<(SkinnedMeshRenderer sourceSmr, string sourcePath)> resolvedSourceSmrs,
			Transform?[] handsNailObjects, Transform?[] leftFootNailObjects, Transform?[] rightFootNailObjects)
		{
			if (resolvedSourceSmrs.Count == 0) return;

			string bakeBasePath = $"{MDNailToolDefines.GENERATED_ASSET_PATH}BlendShapeMesh/{this.AvatarName}/{this.AvatarVariationData.VariationName}";
			var allNailObjects = this.UseFootNail
				? handsNailObjects.Concat(leftFootNailObjects).Concat(rightFootNailObjects)
				: (IEnumerable<Transform?>)handsNailObjects;
			try {
				NailSetupUtil.BakeBlendShapesToNails(
					allNailObjects,
					resolvedSourceSmrs.Select(x => x.sourceSmr),
					bakeBasePath,
					this.AvatarVariationData.BlendShapeInitialWeights);
			} catch (Exception e) {
				ToolConsole.Warn("NailSetup", $"{LanguageManager.S("warn.blendshape_bake_failed") ?? "Failed to bake BlendShapes"}: {e.Message}{BuildDiagnosticInfo()}");
			}
		}
	}
}

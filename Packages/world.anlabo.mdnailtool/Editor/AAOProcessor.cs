#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using world.anlabo.mdnailtool.Runtime;

namespace world.anlabo.mdnailtool.Editor {
	/// <summary>
	/// ビルド時 preprocess 処理(MA/AAO の有無に関係なく動作する VRC SDK 共通エントリ)。
	///
	/// VRC SDK callbackOrder の位置づけ:
	///   -11000     = BuildFrameworkPreprocessHook(NDMF Resolving〜Transforming = MA BoneProxy 等)
	///   -1026      = ★本Processor(NDMF Transforming 完了後 / AAO 実行前)
	///   -1025      = BuildFrameworkOptimizeHook(NDMF Optimizing = AAO 等が走る)
	///   -1024      = ModularAvatar ReplacementRemoveAvatarEditorOnly
	///   Int32.MaxValue = ModularAvatar ReplacementRemoveIEditorOnly
	///
	/// このタイミングで実行されるため、
	/// - MA の BoneProxy はすでに子オブジェクトを指ボーンに移動済み
	/// - MDNailObjectMarker / HandNail_* / FootNail_* ラッパーはまだ生存
	/// - AAO はまだ走っていない
	/// という状態で処理できる。
	///
	/// ここで行うこと:
	/// 1. ビルド診断ログ収集 (Copy Support Info 用、直近1回分を <see cref="LastBuildDiagnostic"/> に保持)
	/// 2. AAO 系コンポーネント検出 (型名・パスのみ、設定値は保持しない)
	/// 3. MDNailObjectMarker 削除 (AAO の TraceAndOptimize unknown-type 警告抑制)
	/// 4. 【非 MA 環境のみ】空ラッパー GO (HandNail_* / FootNail_*) 削除
	///
	/// MA 導入環境ではラッパー GO 自体の破壊は MAPluginDefinition (NDMF Optimizing フェーズ) が
	/// MA 処理完了後に担当する。ここ (-1026) で破壊すると MA の MenuInstaller/ObjectToggle の
	/// 後段処理が未完了でメニュー付きネイルが Play モードで消失するため、MA 環境ではスキップする。
	/// </summary>
	public class AAOProcessor : IVRCSDKPreprocessAvatarCallback {
		public int callbackOrder => -1026;

		/// <summary>直近のビルド時診断ログ (Copy Support Info 用)</summary>
		internal static string? LastBuildDiagnostic { get; private set; }

		/// <summary>診断ログをクリアする (統計情報リセット機能から呼ばれる)</summary>
		internal static void ResetDiagnostic() {
			LastBuildDiagnostic = null;
		}

		public bool OnPreprocessAvatar(GameObject avatarRoot) {
			var sb = new StringBuilder();
			sb.AppendLine($"BuildTime: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

			// ---- AAO 系コンポーネント検出 (型名とパスのみ、設定値は取得しない) ----
			// 詳細設定はユーザーの最適化方針そのものなのでツール側で保持しない。
			// 診断には「AAOが入っているか / どのオブジェクトに付いているか」までで十分。
			var allComponents = avatarRoot.GetComponentsInChildren<MonoBehaviour>(true);
			var aaoComponents = allComponents
				.Where(c => c != null)
				.Where(c => (c.GetType().Namespace ?? "").StartsWith("Anatawa12.AvatarOptimizer"))
				.ToArray();
			if (aaoComponents.Length > 0) {
				sb.AppendLine($"AAOComponents: {aaoComponents.Length}");
				foreach (var c in aaoComponents) {
					string typeName = c.GetType().Name;
					string ownerPath = GetHierarchyPath(c.transform, avatarRoot.transform);
					sb.AppendLine($"  [{typeName}] @ {ownerPath}");
				}
			} else {
				sb.AppendLine("AAOComponents: (none)");
			}

			// ---- マーカー経由の検出（ラッパー単位）----
			var markers = avatarRoot.GetComponentsInChildren<MDNailObjectMarker>(true);
			sb.AppendLine($"NailRootCount: {markers.Length}");
			foreach (MDNailObjectMarker marker in markers) {
				if (marker == null) continue;
				GameObject root = marker.gameObject;
				sb.AppendLine($"  Root: {root.name} (active={root.activeInHierarchy})");

				// BoneProxy 処理済みなら子は空のはず
				int childCount = root.GetComponentsInChildren<Transform>(true).Length - 1;
				sb.AppendLine($"    ChildCount: {childCount}");

				// ラッパー配下の残存 SMR（通常 0 件）
				var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
				sb.AppendLine($"    SMRCount: {smrs.Length}");
				foreach (var smr in smrs) {
					if (smr == null) continue;
					sb.AppendLine(FormatSmrLine("      ", smr));
				}
			}

			// ---- 名前ベースのネイル SMR 追跡（BoneProxy 後は指ボーン配下）----
			var allSmrs = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			var nailNamePatterns = new[] { "HandL.", "HandR.", "FootL.", "FootR." };
			var nailSmrs = allSmrs
				.Where(smr => smr != null && nailNamePatterns.Any(p => smr.gameObject.name.StartsWith(p)))
				.ToArray();
			sb.AppendLine($"NailSmrCount: {nailSmrs.Length}");
			foreach (var smr in nailSmrs) {
				string parentName = smr.transform.parent != null ? smr.transform.parent.name : "(null)";
				sb.AppendLine(FormatSmrLine($"  [{parentName}] ", smr));
			}

			// ---- 残存ラッパーコンテナ（HandNail_* / FootNail_*）----
			var nameBasedWrappers = avatarRoot.GetComponentsInChildren<Transform>(true)
				.Where(t => t != null && (t.name.StartsWith("HandNail_") || t.name.StartsWith("FootNail_")))
				.ToArray();
			sb.AppendLine($"NameBasedWrappers: {nameBasedWrappers.Length}");
			foreach (var t in nameBasedWrappers) {
				sb.AppendLine($"  Wrapper: {t.name} (active={t.gameObject.activeInHierarchy}, childCount={t.childCount}, parent={(t.parent != null ? t.parent.name : "(null)")})");
			}

			LastBuildDiagnostic = sb.ToString();

			// ---- 破壊パス (診断ログ収集完了後) ----
			// AAO の TraceAndOptimize が -1025 で走るため、その前にマーカー "コンポーネント" は
			// 必ず剥がす (AAO の unknown-type 警告を抑制)。GameObject 自体の破壊タイミングは環境で分岐。

#if MD_NAIL_FOR_MA
			// MA 導入環境: コンポーネントのみ剥がす。ラッパー GO の破壊は MAPluginDefinition が
			// NDMF Optimizing フェーズ (MA 処理完了後) に担当する。
			// ここで GameObject ごと破壊すると MA の MenuInstaller/ObjectToggle 後段処理が未完了で
			// メニュー付きネイルが Play モードで消失する。
			foreach (MDNailObjectMarker marker in markers) {
				if (marker != null) Object.DestroyImmediate(marker);
			}
#else
			// 非 MA 環境: MAPluginDefinition は存在しないので、ここでラッパー GO ごと破壊する。
			// 1. マーカー付きラッパー GO を丸ごと破壊
			foreach (MDNailObjectMarker marker in markers) {
				if (marker != null) Object.DestroyImmediate(marker.gameObject);
			}
			// 2. マーカーが既に外れた残存ラッパーも名前で掃除
			foreach (var t in nameBasedWrappers) {
				if (t != null && t.gameObject != null) Object.DestroyImmediate(t.gameObject);
			}
#endif

			return true;
		}

		/// <summary>SMR 1行ぶんの診断文字列を組み立てる</summary>
		private static string FormatSmrLine(string prefix, SkinnedMeshRenderer smr) {
			Bounds b = smr.localBounds;
			int meshVerts = smr.sharedMesh != null ? smr.sharedMesh.vertexCount : -1;
			int matCount = smr.sharedMaterials != null ? smr.sharedMaterials.Length : 0;
			return $"{prefix}{smr.gameObject.name}: enabled={smr.enabled} goActive={smr.gameObject.activeInHierarchy} verts={meshVerts} mats={matCount} uwo={smr.updateWhenOffscreen} bounds=({b.center.x:F2},{b.center.y:F2},{b.center.z:F2})/({b.size.x:F2},{b.size.y:F2},{b.size.z:F2})";
		}

		/// <summary>アバタールートから対象 Transform までのパスを "/" 区切りで返す</summary>
		private static string GetHierarchyPath(Transform t, Transform avatarRoot) {
			if (t == avatarRoot) return "(avatar root)";
			var stack = new Stack<string>();
			Transform? cur = t;
			while (cur != null && cur != avatarRoot) {
				stack.Push(cur.name);
				cur = cur.parent;
			}
			return string.Join("/", stack);
		}

	}
}

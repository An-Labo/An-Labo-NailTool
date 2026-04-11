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
	/// ビルド時 preprocess 処理（MA/AAO の有無に関係なく動作する VRC SDK 共通エントリ）。
	///
	/// VRC SDK callbackOrder の位置づけ：
	///   -11000     = BuildFrameworkPreprocessHook（NDMF Resolving〜Transforming = MA BoneProxy 等）
	///   -1026      = ★本Processor（NDMF Transforming 完了後 / AAO 実行前）
	///   -1025      = BuildFrameworkOptimizeHook（NDMF Optimizing = AAO 等が走る）
	///   -1024      = ModularAvatar ReplacementRemoveAvatarEditorOnly
	///   Int32.MaxValue = ModularAvatar ReplacementRemoveIEditorOnly
	///
	/// このタイミングで実行されるため、
	/// - MA の BoneProxy はすでに子オブジェクトを指ボーンに移動済み
	/// - MDNailObjectMarker / HandNail_* / FootNail_* ラッパーはまだ生存
	/// - AAO はまだ走っていない
	/// という状態で処理できる。
	///
	/// ここで行うこと：
	/// 1. ビルド診断ログ収集（Copy Support Info 用、直近1回分を <see cref="LastBuildDiagnostic"/> に保持）
	/// 2. AAO 系コンポーネントの SerializedObject ダンプ（型依存なし＝AAO 未導入でも安全）
	/// 3. MDNailObjectMarker 削除（AAO の TraceAndOptimize unknown-type 警告抑制）
	/// 4. 空ラッパー GO (HandNail_* / FootNail_*) 削除
	///
	/// MD_NAIL_FOR_MA 等の条件コンパイルに依存せず、MA/AAO/NDMF の有無によらず動作する。
	/// </summary>
	public class AAOProcessor : IVRCSDKPreprocessAvatarCallback {
		public int callbackOrder => -1026;

		/// <summary>直近のビルド時診断ログ（Copy Support Info 用）</summary>
		internal static string? LastBuildDiagnostic { get; private set; }

		public bool OnPreprocessAvatar(GameObject avatarRoot) {
			var sb = new StringBuilder();
			sb.AppendLine($"BuildTime: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

			// ---- AAO 系コンポーネント検出＋設定ダンプ（型依存なし）----
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
					try {
						var so = new SerializedObject(c);
						var iter = so.GetIterator();
						if (iter.NextVisible(true)) {
							do {
								if (iter.propertyPath == "m_Script") continue;
								string value = FormatSerializedProperty(iter);
								sb.AppendLine($"    {iter.propertyPath} = {value}");
							} while (iter.NextVisible(false));
						}
					} catch (System.Exception e) {
						sb.AppendLine($"    (SerializedObject dump failed: {e.Message})");
					}
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

			// ---- 破壊パス（診断ログ収集完了後）----
			// 1. マーカー付きラッパー GO を丸ごと破壊
			foreach (MDNailObjectMarker marker in markers) {
				if (marker != null) Object.DestroyImmediate(marker.gameObject);
			}
			// 2. マーカーが既に外れた（何らかの理由で）残存ラッパーも名前で掃除
			foreach (var t in nameBasedWrappers) {
				if (t != null && t.gameObject != null) Object.DestroyImmediate(t.gameObject);
			}

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

		/// <summary>SerializedProperty を人間可読な文字列に変換（型依存なし）</summary>
		private static string FormatSerializedProperty(SerializedProperty p) {
			switch (p.propertyType) {
				case SerializedPropertyType.Boolean: return p.boolValue.ToString();
				case SerializedPropertyType.Integer: return p.intValue.ToString();
				case SerializedPropertyType.Float: return p.floatValue.ToString("G");
				case SerializedPropertyType.String: return $"\"{p.stringValue}\"";
				case SerializedPropertyType.Enum:
					int idx = p.enumValueIndex;
					if (idx >= 0 && idx < p.enumDisplayNames.Length) return p.enumDisplayNames[idx];
					return idx.ToString();
				case SerializedPropertyType.ObjectReference:
					return p.objectReferenceValue != null ? $"{p.objectReferenceValue.GetType().Name}:{p.objectReferenceValue.name}" : "(null)";
				case SerializedPropertyType.Color: return p.colorValue.ToString();
				case SerializedPropertyType.Vector2: return p.vector2Value.ToString();
				case SerializedPropertyType.Vector3: return p.vector3Value.ToString();
				case SerializedPropertyType.Vector4: return p.vector4Value.ToString();
				case SerializedPropertyType.Bounds: return p.boundsValue.ToString();
				case SerializedPropertyType.Generic:
					// 配列・構造体はプロパティ数だけ書いて中身はインデントして展開
					if (p.isArray) {
						var sb = new StringBuilder();
						sb.Append($"Array[{p.arraySize}]");
						if (p.arraySize > 0 && p.arraySize <= 16) {
							sb.Append(" { ");
							for (int i = 0; i < p.arraySize; i++) {
								if (i > 0) sb.Append(", ");
								var elem = p.GetArrayElementAtIndex(i);
								sb.Append(FormatSerializedProperty(elem));
							}
							sb.Append(" }");
						}
						return sb.ToString();
					}
					// 構造体（DebugOptions 等）は子プロパティを再帰展開
					{
						var sb = new StringBuilder();
						sb.Append("{ ");
						var child = p.Copy();
						var end = p.GetEndProperty();
						bool first = true;
						if (child.NextVisible(true)) {
							do {
								if (SerializedProperty.EqualContents(child, end)) break;
								if (!first) sb.Append(", ");
								first = false;
								sb.Append($"{child.name}={FormatSerializedProperty(child)}");
							} while (child.NextVisible(false));
						}
						sb.Append(" }");
						return sb.ToString();
					}
				default:
					return $"({p.propertyType})";
			}
		}
	}
}

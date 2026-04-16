#nullable enable
#if MD_NAIL_FOR_MA
using System.Linq;
using nadena.dev.ndmf;
using UnityEngine;
[assembly: ExportsPlugin(typeof(world.anlabo.mdnailtool.Editor.MAPluginDefinition))]

namespace world.anlabo.mdnailtool.Editor {
	/// <summary>
	/// MA 導入環境における最終ラッパー破壊パス (NDMF Optimizing フェーズ)。
	///
	/// AAOProcessor (-1026) は NDMF Preprocess(-11000) と NDMF Optimize(-1025) の中間で走るため、
	/// MA の MenuInstaller / ObjectToggle 等の後段処理が未完了でラッパーを壊してしまうと
	/// メニュー付きネイルが Play モードで消失する (0.9.343 で発生した不具合)。
	///
	/// 本 Plugin は NDMF Optimizing フェーズ、すなわち MA の主要処理が完了した状態で走るため、
	/// 空になった HandNail_* / FootNail_* ラッパー GO を安全に破壊できる。
	///
	/// マーカーコンポーネントは AAOProcessor が -1026 で剥がしているため
	/// (AAO の unknown-type 警告抑制のため)、ここでは名前ベースで検出する。
	/// nailRoot 親 (MDNailObjectMarker 剥がれ済み) や [An-Labo] 親は残しても実害がないので触らない。
	///
	/// 非 MA 環境では本ファイルはコンパイルされず (MD_NAIL_FOR_MA 未定義)、
	/// 代わりに AAOProcessor (-1026) がラッパー GO まで破壊する。
	/// </summary>
	public class MAPluginDefinition : Plugin<MAPluginDefinition> {
		public override string QualifiedName => "world.anlabo.mdnailtool";
		public override string DisplayName => "MDNailTool";

		protected override void Configure() {
			InPhase(BuildPhase.Optimizing)
				.Run(DisplayName, context => {
					GameObject avatarRoot = context.AvatarRootObject;

					// 名前ベースで HandNail_* / FootNail_* ラッパー GO を破壊。
					// BoneProxy によって子オブジェクトは指ボーン配下に移動済みなので、
					// ラッパー自身は空 (または MA メニュー用コンポーネントのみ) の状態。
					// ただし「手足まとめる」(BakeBlendShapes=true) 時は統合 SMR が
					// HandNail_<ゾーン名> と同一 GO に存在するため、SMR 持ちは破壊対象から除外する。
					var wrappers = avatarRoot.GetComponentsInChildren<Transform>(true)
						.Where(t => t != null && (t.name.StartsWith("HandNail_") || t.name.StartsWith("FootNail_")))
						.Where(t => t.GetComponent<SkinnedMeshRenderer>() == null)
						.ToArray();
					foreach (Transform t in wrappers) {
						if (t != null && t.gameObject != null) Object.DestroyImmediate(t.gameObject);
					}
				});
		}
	}
}
#endif

#nullable enable
#if MD_NAIL_FOR_MA
using System.Collections.Generic;
using System.Linq;
using System.Text;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using world.anlabo.mdnailtool.Runtime;
[assembly: ExportsPlugin(typeof(world.anlabo.mdnailtool.Editor.MAPluginDefinition))]

namespace world.anlabo.mdnailtool.Editor {
	public class MAPluginDefinition : Plugin<MAPluginDefinition> {

		public override string QualifiedName => "world.anlabo.mdnailtool";
		public override string DisplayName => "MDNailTool";

		/// <summary>直近のビルド時診断ログ（Copy Support Info用）</summary>
		internal static string? LastBuildDiagnostic { get; private set; }

		protected override void Configure() {
			InPhase(BuildPhase.Optimizing)
				.Run(DisplayName, context => {

				var sb = new StringBuilder();
				var markers = context.AvatarRootObject.GetComponentsInChildren<MDNailObjectMarker>(true);
				sb.AppendLine($"NailRootCount: {markers.Length}");

				foreach (MDNailObjectMarker marker in markers.ToArray()) {
					GameObject root = marker.gameObject;
					sb.AppendLine($"  Root: {root.name}");

					// 子オブジェクトの残存チェック（BoneProxy処理済みなら空のはず）
					int childCount = root.GetComponentsInChildren<Transform>(true).Length - 1;
					sb.AppendLine($"    ChildCount: {childCount}");

					// BoneProxy残存チェック
					var remainingBoneProxies = root.GetComponentsInChildren<ModularAvatarBoneProxy>(true);
					sb.AppendLine($"    RemainingBoneProxy: {remainingBoneProxies.Length}");
					foreach (var bp in remainingBoneProxies) {
						sb.AppendLine($"      {bp.gameObject.name} → target={(bp.target != null ? bp.target.name : "(null)")}");
					}

					// ObjectToggle残存チェック
					var remainingToggles = root.GetComponentsInChildren<ModularAvatarObjectToggle>(true);
					sb.AppendLine($"    RemainingObjectToggle: {remainingToggles.Length}");

					Object.DestroyImmediate(root);
				}

				LastBuildDiagnostic = sb.ToString();
				});
		}
	}
}

#endif
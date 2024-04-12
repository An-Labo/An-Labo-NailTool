#if MD_NAIL_FOR_MA
using System.Linq;
using nadena.dev.ndmf;
using UnityEngine;
using world.anlabo.mdnailtool.Runtime;
[assembly: ExportsPlugin(typeof(world.anlabo.mdnailtool.Editor.MAPluginDefinition))]

namespace world.anlabo.mdnailtool.Editor {
	public class MAPluginDefinition : Plugin<MAPluginDefinition> {

		public override string QualifiedName => "world.anlabo.mdnailtool";
		public override string DisplayName => "MDNailTool";

		protected override void Configure() {
			InPhase(BuildPhase.Optimizing)
				.Run(DisplayName, context => {
				
				foreach (MDNailObjectMarker mdNailObjectMarker in context.AvatarRootObject.GetComponentsInChildren<MDNailObjectMarker>().ToArray()) {
					Object.DestroyImmediate(mdNailObjectMarker.gameObject);
				}
				});
		}
	}
}

#endif
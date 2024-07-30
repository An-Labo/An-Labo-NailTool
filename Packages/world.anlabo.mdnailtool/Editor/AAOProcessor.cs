using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using world.anlabo.mdnailtool.Runtime;

namespace world.anlabo.mdnailtool.Editor {
	public class AAOProcessor : IVRCSDKPreprocessAvatarCallback {
		public int callbackOrder => -1025 - 1;

		public bool OnPreprocessAvatar(GameObject avatarGameObject) {
			var components = avatarGameObject.GetComponentsInChildren<MDNailObjectMarker>();
 			foreach (var component in components) {
				Object.DestroyImmediate(component);
			}
			return true;
		}
	}
}

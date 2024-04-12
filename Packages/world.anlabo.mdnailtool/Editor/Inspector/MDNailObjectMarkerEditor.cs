using UnityEditor;
using world.anlabo.mdnailtool.Runtime;

namespace world.anlabo.mdnailtool.Editor.Inspector {
	[CustomEditor(typeof(MDNailObjectMarker))]
	public class MDNailObjectMarkerEditor : UnityEditor.Editor {
		public override void OnInspectorGUI() {
#if MD_NAIL_FOR_MA
			EditorGUILayout.HelpBox(Language.LanguageManager.S("component.md_nail_object_marker.help_box.info"), MessageType.Info);
#else
			EditorGUILayout.HelpBox(Language.LanguageManager.S("component.md_nail_object_marker.help_box.error"), MessageType.Error);
#endif
					}
	}
}
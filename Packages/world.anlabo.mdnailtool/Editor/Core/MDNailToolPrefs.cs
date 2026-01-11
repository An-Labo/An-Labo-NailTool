using UnityEditor;

namespace world.anlabo.mdnailtool.Editor
{
	internal static class MDNailToolPrefs
	{
		private const string HandActiveKey = "MDNailTool_HandActive";
		private const string HandDetailKey = "MDNailTool_HandDetail";
		private const string FootDetailKey = "MDNailTool_FootDetail";

		internal static bool HandActive
		{
			get => EditorPrefs.GetBool(HandActiveKey, true);
			set => EditorPrefs.SetBool(HandActiveKey, value);
		}

		internal static bool HandDetail
		{
			get => EditorPrefs.GetBool(HandDetailKey, false);
			set => EditorPrefs.SetBool(HandDetailKey, value);
		}

		internal static bool FootDetail
		{
			get => EditorPrefs.GetBool(FootDetailKey, false);
			set => EditorPrefs.SetBool(FootDetailKey, value);
		}
	}
}
using UnityEditor;

namespace world.anlabo.mdnailtool.Editor
{
	internal static class MDNailToolPrefs
	{
		private const string HandActiveKey = "MDNailTool_HandActive";
		private const string HandDetailKey = "MDNailTool_HandDetail";
		private const string FootDetailKey = "MDNailTool_FootDetail";
		private const string FingerEnabledMaskKey = "MDNailTool_FingerEnabledMask";
		private const int DefaultFingerEnabledMask = 0xFFFFF;

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

		internal static int FingerEnabledMask
		{
			get => EditorPrefs.GetInt(FingerEnabledMaskKey, DefaultFingerEnabledMask);
			set => EditorPrefs.SetInt(FingerEnabledMaskKey, value);
		}

		internal static bool IsFingerEnabled(int fingerIndex)
		{
			if (fingerIndex < 0 || fingerIndex >= 20) return true;
			return (FingerEnabledMask & (1 << fingerIndex)) != 0;
		}

		internal static void SetFingerEnabled(int fingerIndex, bool enabled)
		{
			if (fingerIndex < 0 || fingerIndex >= 20) return;
			int mask = FingerEnabledMask;
			if (enabled) mask |= (1 << fingerIndex);
			else mask &= ~(1 << fingerIndex);
			FingerEnabledMask = mask;
		}
	}
}
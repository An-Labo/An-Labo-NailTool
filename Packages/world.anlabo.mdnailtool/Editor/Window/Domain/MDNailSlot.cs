#nullable enable

namespace world.anlabo.mdnailtool.Editor.Window.Domain
{
	// 20スロット契約 (手10指 + 足10指) の index 定数。仕様書「20スロット契約」と対応する。
	internal static class MDNailSlot
	{
		internal const int HandCount = 10;
		internal const int FootCount = 10;
		internal const int TotalCount = HandCount + FootCount;
		internal const int HandStartIndex = 0;
		internal const int FootStartIndex = HandCount;
	}
}

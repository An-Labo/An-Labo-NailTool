#nullable enable

using System.Collections.Generic;

namespace world.anlabo.mdnailtool.Editor
{
	// 形のメッシュは全アバター共通の FBX なので、爪先の1つ内側の頂点番号はメッシュ名で決まる。
	internal static class NailTipVertexTable
	{
		private static readonly Dictionary<string, int> Table = Build();

		public static bool TryGet(string meshName, out int index) => Table.TryGetValue(meshName, out index);

		private static Dictionary<string, int> Build()
		{
			var table = new Dictionary<string, int>();
			string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
			foreach (string hand in new[] { "HandL", "HandR" })
			{
				foreach (string finger in fingers)
				{
					table[$"MD_nail_natural_{hand}.{finger}"] = 32;
					table[$"MD_nail_oval_{hand}.{finger}"] = 37;
					table[$"MD_nail_point_{hand}.{finger}"] = 8;
					table[$"MD_nail_square_{hand}.{finger}"] = (finger == "Thumb" || finger == "Little") ? 4 : 6;
				}
			}
			table["MD_nail_natural_HandR.Middle"] = 6;
			table["MD_nail_natural_HandR.Ring"] = 6;
			table["MD_nail_square_HandR.Ring"] = 41;
			return table;
		}
	}
}

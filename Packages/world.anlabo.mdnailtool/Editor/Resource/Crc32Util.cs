using System.IO;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Resource
{
	// CRC32 (zip 互換 polynomial 0xEDB88320) 計算ユーティリティ。Unity 依存なし。
	internal static class Crc32Util
	{
		private static readonly uint[] Table = BuildTable();

		private static uint[] BuildTable()
		{
			uint[] table = new uint[256];
			for (uint i = 0; i < 256; i++)
			{
				uint c = i;
				for (int j = 0; j < 8; j++)
				{
					c = ((c & 1) != 0) ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
				}
				table[i] = c;
			}
			return table;
		}

		internal static uint ComputeFile(string path)
		{
			uint crc = 0xFFFFFFFFu;
			using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				byte[] buffer = new byte[8192];
				int read;
				while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
				{
					for (int i = 0; i < read; i++)
					{
						crc = Table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
					}
				}
			}
			return ~crc;
		}
	}
}

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace world.anlabo.mdnailtool.Editor.Window
{
	internal static class SupportInfoImageRenderer
	{
		private const int ImageWidth = 1200;
		private const int Margin = 42;
		private const int FontSize = 22;

		internal static void SavePng(string supportInfo, string path)
		{
			if (string.IsNullOrWhiteSpace(supportInfo))
				throw new InvalidOperationException("お問い合わせ用情報が空です。");

			Font font = CreateFont();
			font.RequestCharactersInTexture(supportInfo, FontSize, FontStyle.Normal);

			var glyphs = new List<GlyphPlacement>();
			float x = Margin;
			int line = 0;
			foreach (char character in supportInfo.Replace("\r", string.Empty))
			{
				if (character == '\n')
				{
					x = Margin;
					line++;
					continue;
				}

				if (!font.GetCharacterInfo(character, out CharacterInfo info, FontSize, FontStyle.Normal))
					continue;
				float advance = info.advance > 0 ? info.advance : FontSize * 0.5f;
				if (x + advance > ImageWidth - Margin && x > Margin)
				{
					x = Margin;
					line++;
				}
				glyphs.Add(new GlyphPlacement(x, line, info));
				x += advance;
			}

			const int lineHeight = 34;
			int imageHeight = Mathf.Clamp(Margin * 2 + (line + 1) * lineHeight, 320, 8192);
			var output = new Texture2D(ImageWidth, imageHeight, TextureFormat.RGBA32, false);
			try
			{
				Color background = new(0.97f, 0.98f, 1f, 1f);
				Color textColor = new(0.10f, 0.12f, 0.15f, 1f);
				var backgroundPixels = Enumerable.Repeat((Color32)background, ImageWidth * imageHeight).ToArray();
				output.SetPixels32(backgroundPixels);

				Texture2D atlas = (Texture2D)font.material.mainTexture;
				foreach (GlyphPlacement glyph in glyphs)
				{
					CharacterInfo info = glyph.Info;
					int width = Math.Max(1, info.maxX - info.minX);
					int height = Math.Max(1, info.maxY - info.minY);
					int startX = Mathf.RoundToInt(glyph.X) + info.minX;
					int baselineY = imageHeight - Margin - FontSize - glyph.Line * lineHeight;
					int startY = baselineY + info.minY;

					for (int py = 0; py < height; py++)
					{
						float ty = (py + 0.5f) / height;
						for (int px = 0; px < width; px++)
						{
							int destX = startX + px;
							int destY = startY + py;
							if (destX < 0 || destX >= ImageWidth || destY < 0 || destY >= imageHeight) continue;

							float tx = (px + 0.5f) / width;
							Vector2 bottom = Vector2.Lerp(info.uvBottomLeft, info.uvBottomRight, tx);
							Vector2 top = Vector2.Lerp(info.uvTopLeft, info.uvTopRight, tx);
							Vector2 uv = Vector2.Lerp(bottom, top, ty);
							float alpha = atlas.GetPixelBilinear(uv.x, uv.y).a;
							if (alpha > 0.01f) output.SetPixel(destX, destY, Color.Lerp(background, textColor, alpha));
						}
					}
				}
				output.Apply();

				byte[] png = output.EncodeToPNG();
				string? directory = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
				File.WriteAllBytes(path, png);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(output);
				UnityEngine.Object.DestroyImmediate(font);
			}
		}

		private readonly struct GlyphPlacement
		{
			internal readonly float X;
			internal readonly int Line;
			internal readonly CharacterInfo Info;

			internal GlyphPlacement(float x, int line, CharacterInfo info)
			{
				X = x;
				Line = line;
				Info = info;
			}
		}

		private static Font CreateFont()
		{
			string[] installed = Font.GetOSInstalledFontNames();
			string? fontName = new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo", "Arial" }
				.FirstOrDefault(candidate => installed.Any(name => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)));
			fontName ??= installed.FirstOrDefault();
			if (string.IsNullOrWhiteSpace(fontName))
				throw new InvalidOperationException("画像生成に使用できるフォントが見つかりませんでした。");
			return Font.CreateDynamicFontFromOSFont(fontName, FontSize);
		}
	}
}

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Tests
{
	/// <summary>
	/// MDNailToolAssetLoaderのGUID解決とフォールバック動作を検証する。
	/// </summary>
	public class AssetLoaderTests
	{
		[Test]
		public void LoadByGuid_ValidGuid_ReturnsAsset()
		{
			// WindowUssは常にパッケージ内に存在する
			var asset = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(MDNailToolGuids.WindowUss);
			Assert.That(asset, Is.Not.Null, "有効なGUIDでUSSが読み込めない");
		}

		[Test]
		public void LoadByGuid_InvalidGuid_WithFallbackPath_ReturnsAsset()
		{
			string invalidGuid = "00000000000000000000000000000000";
			string fallbackPath = MDNailToolGuids.WindowUssPath;
			var asset = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(invalidGuid, fallbackPath);
			Assert.That(asset, Is.Not.Null, "無効なGUIDでもフォールバックパスから読み込めるべき");
		}

		[Test]
		public void LoadByGuid_InvalidGuid_WithoutFallback_ReturnsNull()
		{
			// PathHint汚染を避けるため他テストと異なるGUIDを使用
			string invalidGuid = "ffff0000ffff0000ffff0000ffff0000";
			var asset = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(invalidGuid);
			Assert.That(asset, Is.Null, "無効なGUIDでフォールバックなしならnullを返すべき");
		}

		[Test]
		public void LoadByGuid_FallbackPath_RegistersPathHint()
		{
			string invalidGuid = "aaaabbbbccccddddeeee111122223333";
			string fallbackPath = MDNailToolGuids.WindowUssPath;

			// フォールバック経由でロード
			var asset1 = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(invalidGuid, fallbackPath);
			Assert.That(asset1, Is.Not.Null, "フォールバック経由で読み込めるべき");

			// PathHintが登録されたので、フォールバックなしでも読み込める
			var asset2 = MDNailToolAssetLoader.LoadByGuid<StyleSheet>(invalidGuid);
			Assert.That(asset2, Is.Not.Null, "PathHint登録後はフォールバックなしでも読み込めるべき");
		}

		[Test]
		public void ResolveGuidToPath_ValidGuid_ReturnsPath()
		{
			string? path = MDNailToolAssetLoader.ResolveGuidToPath(MDNailToolGuids.WindowUss);
			Assert.That(path, Is.Not.Null.And.Not.Empty, "有効なGUIDでパス解決できない");
		}

		[Test]
		public void ResolveGuidToPath_NullGuid_WithFallback_ReturnsFallback()
		{
			string fallback = MDNailToolGuids.WindowUssPath;
			string? path = MDNailToolAssetLoader.ResolveGuidToPath(null, fallback);
			Assert.That(path, Is.EqualTo(fallback), "GUIDがnullならフォールバックパスを返すべき");
		}

		[Test]
		public void LoadShader_PreviewShader_Loads()
		{
			Shader? shader = MDNailToolAssetLoader.LoadShader(MDNailToolDefines.PREVIEW_SHADER_GUID);
			Assert.That(shader, Is.Not.Null, "PreviewShaderが読み込めない");
		}

		[Test]
		public void LoadShader_GrayShader_Loads()
		{
			Shader? shader = MDNailToolAssetLoader.LoadShader(MDNailToolDefines.GRAY_SHADER_GUID);
			Assert.That(shader, Is.Not.Null, "GrayShaderが読み込めない");
		}

		[Test]
		public void LoadShader_InvalidGuid_FallsBackToShaderFind()
		{
			Shader? shader = MDNailToolAssetLoader.LoadShader(
				"00000000000000000000000000000000",
				"Hidden/Internal-Colored");
			Assert.That(shader, Is.Not.Null, "Shader.Findフォールバックが機能していない");
		}

		[Test]
		public void LoadThumbnail_ExistingDesign_ReturnsThumbnail()
		{
			// 最初のデザイン名を取得してサムネイルテスト
			using var db = new Model.DBNailDesign();
			Entity.NailDesign? firstDesign = null;
			foreach (var d in db.collection)
			{
				if (!string.IsNullOrEmpty(d.ThumbnailGUID))
				{
					firstDesign = d;
					break;
				}
			}

			if (firstDesign == null)
			{
				Assert.Ignore("ThumbnailGUIDを持つデザインがない（テストスキップ）");
				return;
			}

			Texture2D? thumbnail = MDNailToolAssetLoader.LoadThumbnail(
				firstDesign.ThumbnailGUID, firstDesign.DesignName);
			Assert.That(thumbnail, Is.Not.Null,
				$"デザイン '{firstDesign.DesignName}' のサムネイルが読み込めない（GUID={firstDesign.ThumbnailGUID}）");
		}
	}
}

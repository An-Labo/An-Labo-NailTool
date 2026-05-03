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
		public void LoadAssetSafe_NullPath_ReturnsNull()
		{
			StyleSheet? asset = MDNailToolAssetLoader.LoadAssetSafe<StyleSheet>(null);
			Assert.That(asset, Is.Null, "null パスで null を返すべき");
		}

		[Test]
		public void LoadAssetSafe_EmptyPath_ReturnsNull()
		{
			StyleSheet? asset = MDNailToolAssetLoader.LoadAssetSafe<StyleSheet>("");
			Assert.That(asset, Is.Null, "空パスで null を返すべき");
		}

		[Test]
		public void LoadAssetSafe_ValidPath_ReturnsAsset()
		{
			StyleSheet? asset = MDNailToolAssetLoader.LoadAssetSafe<StyleSheet>(MDNailToolGuids.WindowUssPath);
			Assert.That(asset, Is.Not.Null, "有効パスでアセットが取得できるべき");
		}

		[Test]
		public void LoadAssetSafe_NonexistentPath_ReturnsNull()
		{
			StyleSheet? asset = MDNailToolAssetLoader.LoadAssetSafe<StyleSheet>("Assets/__not_exist__.uss");
			Assert.That(asset, Is.Null, "存在しないパスで null を返すべき");
		}

		[Test]
		public void LoadAssetSafe_PathWithBrackets_DoesNotThrow()
		{
			// `[]` 含むパスで例外を起こさず graceful に null/asset を返すこと (0.9.383 事故防止)
			Assert.DoesNotThrow(() => {
				MDNailToolAssetLoader.LoadAssetSafe<StyleSheet>("Assets/[Test]Foo.uss");
			});
		}

		[Test]
		public void LoadPrefabSafe_NullPath_ReturnsNull()
		{
			GameObject? prefab = MDNailToolAssetLoader.LoadPrefabSafe(null);
			Assert.That(prefab, Is.Null, "null パスで null を返すべき");
		}

		[Test]
		public void LoadPrefabSafe_EmptyPath_ReturnsNull()
		{
			GameObject? prefab = MDNailToolAssetLoader.LoadPrefabSafe("");
			Assert.That(prefab, Is.Null, "空パスで null を返すべき");
		}

		[Test]
		public void LoadPrefabSafe_PathWithBrackets_DoesNotThrow()
		{
			Assert.DoesNotThrow(() => {
				MDNailToolAssetLoader.LoadPrefabSafe("Assets/[Test]Foo.prefab");
			});
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

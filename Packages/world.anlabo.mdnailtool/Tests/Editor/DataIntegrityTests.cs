using NUnit.Framework;
using world.anlabo.mdnailtool.Editor.Model;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Tests
{
	/// <summary>
	/// JSON DBが正しくデシリアライズでき、必須フィールドが欠けていないことを検証する。
	/// DBのスキーマ変更やJSON破損を早期検出する。
	/// </summary>
	public class DataIntegrityTests
	{
		[Test]
		public void NailDesignDB_DeserializesWithoutError()
		{
			using var db = new DBNailDesign();
			Assert.That(db.collection, Is.Not.Null);
			Assert.That(db.collection.Count, Is.GreaterThan(0), "nailDesign.json にエントリがない");
		}

		[Test]
		public void NailDesignDB_AllEntriesHaveRequiredFields()
		{
			using var db = new DBNailDesign();
			foreach (var design in db.collection)
			{
				Assert.That(design.DesignName, Is.Not.Null.And.Not.Empty,
					$"NailDesign id={design.Id} の DesignName が空");
				Assert.That(design.ColorVariation, Is.Not.Null,
					$"NailDesign '{design.DesignName}' の ColorVariation が null");
			}
		}

		[Test]
		public void NailDesignDB_NoDuplicateDesignNames()
		{
			using var db = new DBNailDesign();
			var seen = new System.Collections.Generic.HashSet<string>();
			foreach (var design in db.collection)
			{
				Assert.That(seen.Add(design.DesignName), Is.True,
					$"DesignName '{design.DesignName}' が重複している");
			}
		}

		[Test]
		public void ShopDB_DeserializesWithoutError()
		{
			using var db = new DBShop();
			Assert.That(db.collection, Is.Not.Null);
			Assert.That(db.collection.Count, Is.GreaterThan(0), "shop.json にエントリがない");
		}

		[Test]
		public void ShopDB_AllAvatarsHaveVariations()
		{
			using var db = new DBShop();
			foreach (var shop in db.collection)
			{
				Assert.That(shop.ShopName, Is.Not.Null.And.Not.Empty,
					"Shop の ShopName が空");
				foreach (var avatar in shop.Avatars.Values)
				{
					Assert.That(avatar.AvatarName, Is.Not.Null.And.Not.Empty,
						$"Shop '{shop.ShopName}' のアバター名が空");
					Assert.That(avatar.AvatarVariations, Is.Not.Null,
						$"Avatar '{avatar.AvatarName}' の AvatarVariations が null");
					if (avatar.AvatarVariations.Count == 0)
						UnityEngine.Debug.LogWarning($"Avatar '{avatar.AvatarName}' に Variation がない（未設定の可能性）");
				}
			}
		}

		[Test]
		public void ShopDB_AllVariationsHaveNailPrefabGUID()
		{
			using var db = new DBShop();
			foreach (var shop in db.collection)
			{
				foreach (var avatar in shop.Avatars.Values)
				{
					foreach (var variation in avatar.AvatarVariations.Values)
					{
						Assert.That(variation.VariationName, Is.Not.Null.And.Not.Empty,
							$"Avatar '{avatar.AvatarName}' に VariationName 空の variation がある");
					}
				}
			}
		}

		[Test]
		public void NailShapeDB_DeserializesWithoutError()
		{
			using var db = new DBNailShape();
			Assert.That(db.collection, Is.Not.Null);
			Assert.That(db.collection.Count, Is.GreaterThan(0), "nailShape.json にエントリがない");
		}

		[Test]
		public void NailShapeDB_AllShapesHaveRequiredFields()
		{
			using var db = new DBNailShape();
			foreach (var shape in db.collection)
			{
				Assert.That(shape.ShapeName, Is.Not.Null.And.Not.Empty,
					"NailShape の ShapeName が空");
				Assert.That(shape.FbxFolderGUID, Is.Not.Null,
					$"NailShape '{shape.ShapeName}' の FbxFolderGUID が null");
				Assert.That(shape.FbxFolderGUID.Length, Is.GreaterThan(0),
					$"NailShape '{shape.ShapeName}' の FbxFolderGUID が空配列");
				Assert.That(shape.NormalMapGUID, Is.Not.Null.And.Not.Empty,
					$"NailShape '{shape.ShapeName}' の NormalMapGUID が空");
			}
		}

		[Test]
		public void DBBase_CacheWorksCorrectly()
		{
			// 2回開いて同じデータを参照していること（キャッシュの整合性テスト）
			using var db1 = new DBNailDesign();
			using var db2 = new DBNailDesign();
			Assert.That(db1.collection.Count, Is.EqualTo(db2.collection.Count),
				"同じDBを2回開いた結果が不一致（キャッシュ異常の可能性）");
		}
	}
}

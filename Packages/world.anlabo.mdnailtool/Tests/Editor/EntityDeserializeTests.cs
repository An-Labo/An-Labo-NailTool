using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using world.anlabo.mdnailtool.Editor.Entity;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Tests
{
	/// <summary>
	/// Entity クラスの JSON デシリアライズが期待通り動作するか検証する。
	/// JSONスキーマの変更やEntityクラスの変更が壊れていないことを早期検出する。
	/// </summary>
	public class EntityDeserializeTests
	{
		[Test]
		public void NailDesign_Deserialize_MinimalJson()
		{
			string json = @"{
				""TestDesign"": {
					""id"": 1,
					""designName"": ""TestDesign"",
					""colorVariation"": {
						""Default"": { ""colorName"": ""Default"" }
					}
				}
			}";

			var result = JsonConvert.DeserializeObject<Dictionary<string, NailDesign>>(json);
			Assert.That(result, Is.Not.Null);
			Assert.That(result!.ContainsKey("TestDesign"), Is.True);
			Assert.That(result["TestDesign"].DesignName, Is.EqualTo("TestDesign"));
			Assert.That(result["TestDesign"].Id, Is.EqualTo(1));
			Assert.That(result["TestDesign"].ColorVariation, Is.Not.Null);
			Assert.That(result["TestDesign"].ColorVariation.Count, Is.EqualTo(1));
		}

		[Test]
		public void NailDesign_Deserialize_OptionalFieldsCanBeNull()
		{
			string json = @"{
				""Test"": {
					""id"": 0,
					""designName"": ""Test"",
					""colorVariation"": {}
				}
			}";

			var result = JsonConvert.DeserializeObject<Dictionary<string, NailDesign>>(json);
			Assert.That(result, Is.Not.Null);
			var design = result!["Test"];
			Assert.That(design.ThumbnailGUID, Is.Null);
			Assert.That(design.TagColor, Is.Null);
			Assert.That(design.AdditionalMaterialGUIDs, Is.Null);
			Assert.That(design.AdditionalObjectGUIDs, Is.Null);
			Assert.That(design.ParentVariant, Is.Null);
		}

		[Test]
		public void NailShape_Deserialize()
		{
			string json = @"{
				""Natural"": {
					""shapeName"": ""Natural"",
					""fbxFolderGUID"": [""guid1"", ""guid2""],
					""fbxNamePrefix"": ""[Natural]"",
					""footFbxFolderGUID"": [""guid3""],
					""footFbxNamePrefix"": ""[Natural]"",
					""normalMapGUID"": ""guid4""
				}
			}";

			var result = JsonConvert.DeserializeObject<Dictionary<string, NailShape>>(json);
			Assert.That(result, Is.Not.Null);
			var shape = result!["Natural"];
			Assert.That(shape.ShapeName, Is.EqualTo("Natural"));
			Assert.That(shape.FbxFolderGUID.Length, Is.EqualTo(2));
			Assert.That(shape.NormalMapGUID, Is.EqualTo("guid4"));
		}

		[Test]
		public void AvatarVariation_Deserialize_WithBlendShapeVariants()
		{
			string json = @"{
				""Default"": {
					""variationName"": ""Default"",
					""nailPrefabGUID"": ""prefab-guid"",
					""avatarPrefabs"": [{ ""prefabGUID"": ""p1"" }],
					""avatarFbxs"": [{ ""fbxGUID"": ""f1"" }],
					""blendShapeVariants"": [
						{
							""name"": ""Variant1"",
							""nailPrefabGUID"": ""var-guid""
						}
					]
				}
			}";

			var result = JsonConvert.DeserializeObject<Dictionary<string, AvatarVariation>>(json);
			Assert.That(result, Is.Not.Null);
			var variation = result!["Default"];
			Assert.That(variation.VariationName, Is.EqualTo("Default"));
			Assert.That(variation.NailPrefabGUID, Is.EqualTo("prefab-guid"));
			Assert.That(variation.BlendShapeVariants, Is.Not.Null);
			Assert.That(variation.BlendShapeVariants!.Length, Is.EqualTo(1));
			Assert.That(variation.BlendShapeVariants[0].Name, Is.EqualTo("Variant1"));
		}

		[Test]
		public void Shop_Deserialize_NestedStructure()
		{
			string json = @"{
				""TestShop"": {
					""shopName"": ""TestShop"",
					""avatars"": {
						""TestAvatar"": {
							""avatarName"": ""TestAvatar"",
							""avatarVariations"": {
								""Default"": {
									""variationName"": ""Default"",
									""nailPrefabGUID"": ""guid1"",
									""avatarPrefabs"": [],
									""avatarFbxs"": []
								}
							}
						}
					}
				}
			}";

			var result = JsonConvert.DeserializeObject<Dictionary<string, Shop>>(json);
			Assert.That(result, Is.Not.Null);
			var shop = result!["TestShop"];
			Assert.That(shop.ShopName, Is.EqualTo("TestShop"));
			Assert.That(shop.Avatars.Count, Is.EqualTo(1));

			var avatar = shop.FindAvatarByName("TestAvatar");
			Assert.That(avatar, Is.Not.Null);
			Assert.That(avatar!.AvatarVariations.Count, Is.EqualTo(1));
		}

		[Test]
		public void NailDesign_FindVariationByName_ReturnsCorrectVariation()
		{
			string json = @"{
				""Test"": {
					""id"": 0,
					""designName"": ""Test"",
					""colorVariation"": {
						""Red"": { ""colorName"": ""Red"" },
						""Blue"": { ""colorName"": ""Blue"" }
					}
				}
			}";

			var result = JsonConvert.DeserializeObject<Dictionary<string, NailDesign>>(json);
			var design = result!["Test"];

			Assert.That(design.FindVariationByName("Red"), Is.Not.Null);
			Assert.That(design.FindVariationByName("Blue"), Is.Not.Null);
			Assert.That(design.FindVariationByName("Green"), Is.Null);
			Assert.That(design.FindVariationByName(null), Is.Null);
		}
	}
}

// #define MD_NAIL_DEVELOP

#if MD_NAIL_DEVELOP
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace world.anlabo.mdnailtool.Editor.Develop {
	internal static class Script {
		
		[MenuItem("An-Labo/Develop/ExportNailDesign")]
		private static void ExportNailDesign() {
			const string DESIGN_GUID = "68ce78b1ee1e48d69b08ae40d18a7547";
			string designDir = AssetDatabase.GUIDToAssetPath(DESIGN_GUID);
			
			foreach (string dir in Directory.EnumerateDirectories(designDir)) {
				string naturalDir = $@"{dir}\Natural";
				if (!Directory.Exists(naturalDir)) {
					Debug.LogError(@$"Not found : {naturalDir}");
					continue;
				}

				string dirName = Path.GetFileName(dir);
				string materialPath = $@"{naturalDir}\[mat][{dirName}][lil-toon]natural_Thumb.L.mat";

				if (!File.Exists(materialPath)) {
					Debug.LogError(@$"Not found : {materialPath}");
					continue;
				}
				
				File.Copy(materialPath, $@"{dir}\{dirName}.mat");
				
				Debug.Log(materialPath);
			}
		}
		
		[MenuItem("An-Labo/Develop/ExportThumbnailGUID")]
		private static void ExportThumbnailGUID() {
			const string JSON_GUID = "51109fbdaae74ad0aada0a6dd6196998";
			const string THUMBNAIL_DIR_GUID = "9858668b2ba7a72428956178ea758236";
			string thumbnailDir = AssetDatabase.GUIDToAssetPath(THUMBNAIL_DIR_GUID);
			
			string json = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(JSON_GUID)).text;
			JArray jArray = JArray.Parse(json);

			List<string> guidList = new();
			List<string> nailList = new();
			foreach (JObject nail in jArray.Cast<JObject>()) {
				string nailName = nail["nailName"]?.Value<string>();
				if (nailName == null) {
					Debug.Log("error");
					continue;
				}

				string thumbnailPath = $"{thumbnailDir}/{nailName}.jpg";
				if (!File.Exists(thumbnailPath)) {
					Debug.Log($"NotFound : {thumbnailPath}");
					continue;
				}

				string guid = AssetDatabase.AssetPathToGUID(thumbnailPath);
				if (!AssetDatabase.GUIDToAssetPath(guid).Equals(thumbnailPath)) {
					Debug.Log($"error : {thumbnailPath} : {AssetDatabase.GUIDToAssetPath(guid)}");
				} else {
					guidList.Add(guid);
					nailList.Add(nailName);
				}
			}
			
			
			StringBuilder stringBuilder = new();
			stringBuilder.AppendLine("{");
			int i = 0;
			foreach (string guid in guidList) {
				stringBuilder.AppendLine($"  \"{nailList[i]}\": \"{guid}\",");
				i++;
			}

			stringBuilder.AppendLine("}");

			Debug.Log(stringBuilder.ToString());

		}

		[MenuItem("An-Labo/Develop/ExportPrefabGUID")]
		private static void ExportPrefabGUID() {
			const string DIR_GUID = "a9a4e7beff717c84da6998bc7861d517";
			string[] DIRS = {
				"リーファ",
				"リーファ",
				"まりえる",
				"ウルフェリア",
				"京狐",
				"シュティーア",
				"京狐",
				"シリウス",
				"透羽",
				"薄荷",
				"桔梗",
				"桔梗old",
				"メリノ",
				"LSBody",
				"カリン",
				"あまなつ",
				"あまなつ",
				"舞夜",
				"ここあ",
				"狐雪",
				"Moe",
				"くろなつ",
				"竜胆",
				"イメリス",
				"イヨ",
				"あのん",
				"Grus",
				"GrusT",
				"Ash",
				"Ash",
				"リアアリス",
				"ルルリア",
				"ヨール",
				"ルシナ",
				"ヨール",
				"Rico",
				"碼希",
				"しずくさん",
				"水瀬",
				"セレスティア",
				"Nayu",
				"Nayu",
				"ハオラン",
				"Peke",
				"ネーヴェ",
				"ゾメちゃん",
				"チセ",
				"エーデルワイス",
				"NewNecoMaid",
				"ソフィナ",
				"サフィー",
				"サフィー",
				"ニャスカ！",
				"ミルク_Re",
				"TubeRose",
				"ルーシュカ",
				"フィオナ",
				"Glaze",
				"サタリナ",
				"MARUBODY",
				"MARUBODY",
				"MARUBODY",
				"MARUBODY",
				"ミント",
				"ティナ",
				"ミラ",
				"AAbody_A",
				"AAbody_T",
				"レイ",
				"めいゆん",
				"Seiren",
				"森羅",
				"杏里",
				"杏里_men",
				"Moe_Highheeled_OFF",
				"Moe_Highheeled_ON",
				"舞夜_Highheeled",
				"森羅_Foot_highheel",
				"ラシューシャ",
				"ラシューシャ_ヒールなし",
				"リーファ_スニーカー",
				"リーファ_ビーチサンダル",
				"リーファ_高ヒール",
				"リーファ_スニーカー",
				"リーファ_ビーチサンダル",
				"リーファ_高ヒール",
				"ライム",
				"Zange",
				"椎名",
				"デルタフレア",
				"ソラハ",
				"ルフィナ",
				"シェーナ",
				"ルゼブル",
				"エミスティア",
				"ましゅめろ",
				"椎名",
				"椎名",
				"ライム_Foot_HighHeel",
				"Lapwing",
				"LapwingT",
				"マヌカ",
				"マヌカ_Foot_heel",
				"Yoll",
				"瑞希",
				"Hakua",
				"Hakua_Heel",
				"Hakua_HighHeel",
				"Komano",
				"NagiyaRuri",
				"Nagi",
				"Nagi_loli",
				"Itousan",
				"Kuuta",
				"Mafuyu",
				"Masscat2",
				"Ururu",
				"LIMILIA",
				"Tien",
				"Chiffon",
				"椿姫",
				"椿姫_Option_Boots",
				"姫華_A",
				"姫華_T",
				"Noy",
				"PlusHead",
				"U_Body",
				"トラス",
				"Yuuko",
				"Lio",
				"Sakuya",
				"Bihou",
				"capra",
				"猫山宙",
				"猫山宙_highheele",
				"U",
				"ユギミヨ"
			};

			List<string> guidList = new();

			string rootPath = AssetDatabase.GUIDToAssetPath(DIR_GUID);
			foreach (string dir in DIRS) {
				string prefabDir = $"{rootPath}/{dir}";
				string prefabPath = $"{prefabDir}/[Natural]{dir}.prefab";

				if (Directory.Exists(prefabDir) && File.Exists(prefabPath)) {
					string guid = AssetDatabase.AssetPathToGUID(prefabPath);
					if (!AssetDatabase.GUIDToAssetPath(guid).Equals(prefabPath)) {
						Debug.Log($"error : {prefabPath} : {AssetDatabase.GUIDToAssetPath(guid)}");
					} else {
						guidList.Add(guid);
					}
				} else {
					Debug.Log($"Not found File : {prefabPath}");
				}
			}

			StringBuilder stringBuilder = new();
			stringBuilder.AppendLine("[");
			foreach (string guid in guidList) {
				stringBuilder.AppendLine($"  \"{guid}\",");
			}

			stringBuilder.AppendLine("]");

			Debug.Log(stringBuilder.ToString());
		}

	}
}

#endif
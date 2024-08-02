using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace world.anlabo.mdnailtool.Editor {
	[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
	public static class MDNailToolDefines {
		public const string ROOT_ASSET_PATH = "Assets/[An-Labo.Virtual]/An-Labo Nail Tool/";
		public const string GENERATED_ASSET_PATH = ROOT_ASSET_PATH + "Generated/";
		public const string BACKUP_PATH = ROOT_ASSET_PATH + "Backup/";
		public const string REPORT_PATH = ROOT_ASSET_PATH + "Report/";

		public const string ROOT_PACKAGE_PATH = "Packages/world.anlabo.mdnailtool/";
		public const string RESOURCE_PATH = ROOT_PACKAGE_PATH + "Resource/";
		public const string LANG_FILE_PATH = RESOURCE_PATH + "Lang/langs.json";
		public const string DB_PATH = RESOURCE_PATH + "DB/";
		public const string DB_SHOP_FILE_PATH = DB_PATH + "shop.json";
		public const string DB_NAIL_SHAPE_FILE_PATH = DB_PATH + "nailShape.json";
		public const string DB_NAIL_DESIGN_FILE_PATH = DB_PATH + "nailDesign.json";
		public const string NAIL_DESIGN_PATH = RESOURCE_PATH + "Nail/Design/";

		public const string LEGACY_DESIGN_PATH = "Assets/[An-Labo.Virtual]/【Nail】/";

		public const string PREVIEW_SHADER_GUID = "5f0e4274bce4492f833972c906cd3236";
		public const string PREVIEW_PREFAB_GUID = "371bb8ffdf995444b823f1f4a1111dcb";

		public const string GRAY_SHADER_GUID = "57969ef515a043528c7c5e39cb29d123";

		public static readonly string[] FOOT_NAIL_CHIP_FOLDER_GUIDS = {
			"37d4110791f04a447af17c82ee9d77a4", 
			"15de7a83bfbe0504fb98ebbac9afbd92"
		};

		public const string LEFT_THUMB_DISTAL = "Left Thumb Distal";
		public const string LEFT_INDEX_DISTAL = "Left Index Distal";
		public const string LEFT_MIDDLE_DISTAL = "Left Middle Distal";
		public const string LEFT_RING_DISTAL = "Left Ring Distal";
		public const string LEFT_LITTLE_DISTAL = "Left Little Distal";


		public const string RIGHT_THUMB_DISTAL = "Right Thumb Distal";
		public const string RIGHT_INDEX_DISTAL = "Right Index Distal";
		public const string RIGHT_MIDDLE_DISTAL = "Right Middle Distal";
		public const string RIGHT_RING_DISTAL = "Right Ring Distal";
		public const string RIGHT_LITTLE_DISTAL = "Right Little Distal";

		public const string LEFT_FOOT = "LeftFoot";
		public const string LEFT_TOES = "LeftToes";
		public const string RIGHT_FOOT = "RightFoot";
		public const string RIGHT_TOES = "RightToes";
		
		public const string HAND_L_THUMB = "HandL.Thumb";
		public const string HAND_L_INDEX = "HandL.Index";
		public const string HAND_L_MIDDLE = "HandL.Middle";
		public const string HAND_L_RING = "HandL.Ring";
		public const string HAND_L_LITTLE = "HandL.Little";

		public const string HAND_R_THUMB = "HandR.Thumb";
		public const string HAND_R_INDEX = "HandR.Index";
		public const string HAND_R_MIDDLE = "HandR.Middle";
		public const string HAND_R_RING = "HandR.Ring";
		public const string HAND_R_LITTLE = "HandR.Little";

		public const string FOOT_L_THUMB = "FootL.Thumb";
		public const string FOOT_L_INDEX = "FootL.Index";
		public const string FOOT_L_MIDDLE = "FootL.Middle";
		public const string FOOT_L_RING = "FootL.Ring";
		public const string FOOT_L_LITTLE = "FootL.Little";
		
		public const string FOOT_R_THUMB = "FootR.Thumb";
		public const string FOOT_R_INDEX = "FootR.Index";
		public const string FOOT_R_MIDDLE = "FootR.Middle";
		public const string FOOT_R_RING = "FootR.Ring";
		public const string FOOT_R_LITTLE = "FootR.Little";


		public enum TargetFingerAndToe {
			All = -1,
			LeftThumb = 0,
			LeftIndex = 1,
			LeftMiddle = 2,
			LeftRing = 3,
			LeftLittle = 4,
			RightThumb = 5,
			RightIndex = 6,
			RightMiddle = 7,
			RightRing = 8,
			RightLittle = 9,
			LeftToes = 10,
			RightToes = 11
		}
		
		public enum TargetFinger {
			All = -1,
			LeftThumb = 0,
			LeftIndex = 1,
			LeftMiddle = 2,
			LeftRing = 3,
			LeftLittle = 4,
			RightThumb = 5,
			RightIndex = 6,
			RightMiddle = 7,
			RightRing = 8,
			RightLittle = 9
		}

		public static readonly IReadOnlyList<HumanBodyBones> HANDS_HUMAN_BODY_BONE_LIST = Array.AsReadOnly(new[] {
			HumanBodyBones.LeftThumbDistal,
			HumanBodyBones.LeftIndexDistal,
			HumanBodyBones.LeftMiddleDistal,
			HumanBodyBones.LeftRingDistal,
			HumanBodyBones.LeftLittleDistal,
			HumanBodyBones.RightThumbDistal,
			HumanBodyBones.RightIndexDistal,
			HumanBodyBones.RightMiddleDistal,
			HumanBodyBones.RightRingDistal,
			HumanBodyBones.RightLittleDistal,
		});

		public const HumanBodyBones LEFT_TOE_HUMAN_BODY_BONE = HumanBodyBones.LeftToes;
		public const HumanBodyBones RIGHT_TOE_HUMAN_BODY_BONE = HumanBodyBones.RightToes;

		public static readonly IReadOnlyList<string> TARGET_BONE_NAME_LIST = Array.AsReadOnly( new[] {
			LEFT_THUMB_DISTAL,
			LEFT_INDEX_DISTAL,
			LEFT_MIDDLE_DISTAL,
			LEFT_RING_DISTAL,
			LEFT_LITTLE_DISTAL,
			RIGHT_THUMB_DISTAL,
			RIGHT_INDEX_DISTAL,
			RIGHT_MIDDLE_DISTAL,
			RIGHT_RING_DISTAL,
			RIGHT_LITTLE_DISTAL,
			LEFT_TOES,
			RIGHT_TOES
		});
		
		public static readonly IReadOnlyList<string> TARGET_HANDS_BONE_NAME_LIST = Array.AsReadOnly( new[] {
			LEFT_THUMB_DISTAL,
			LEFT_INDEX_DISTAL,
			LEFT_MIDDLE_DISTAL,
			LEFT_RING_DISTAL,
			LEFT_LITTLE_DISTAL,
			RIGHT_THUMB_DISTAL,
			RIGHT_INDEX_DISTAL,
			RIGHT_MIDDLE_DISTAL,
			RIGHT_RING_DISTAL,
			RIGHT_LITTLE_DISTAL,
		});

		public static readonly IReadOnlyList<string> HANDS_NAIL_OBJECT_NAME_LIST = Array.AsReadOnly(new[] {
			HAND_L_THUMB ,
			HAND_L_INDEX,
			HAND_L_MIDDLE,
			HAND_L_RING ,
			HAND_L_LITTLE,
			HAND_R_THUMB,
			HAND_R_INDEX,
			HAND_R_MIDDLE,
			HAND_R_RING,
			HAND_R_LITTLE
		});

		public static readonly IReadOnlyList<string> LEFT_FOOT_NAIL_OBJECT_NAME_LIST = Array.AsReadOnly(new[] {
			FOOT_L_THUMB,
			FOOT_L_INDEX,
			FOOT_L_MIDDLE,
			FOOT_L_RING,
			FOOT_L_LITTLE
		});
		
		public static readonly IReadOnlyList<string> RIGHT_FOOT_NAIL_OBJECT_NAME_LIST = Array.AsReadOnly(new[] {
			FOOT_R_THUMB,
			FOOT_R_INDEX,
			FOOT_R_MIDDLE,
			FOOT_R_RING,
			FOOT_R_LITTLE
		});


		private const string PACKAGE_JSON_GUID = "ebc756db0be878d4a9e25917bfb98ab4";
		public static string Version {
			get {
				string packagePath = AssetDatabase.GUIDToAssetPath(PACKAGE_JSON_GUID);
				JObject package = JObject.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(packagePath).text);
				return package.GetValue("version")!.Value<string>();
			}
		}



	}
}
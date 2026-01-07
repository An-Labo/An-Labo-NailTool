using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Runtime.Components;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Inspector {

    [CustomEditor(typeof(MDNailAutoInstaller))]
    public class MDNailAutoInstallerEditor : UnityEditor.Editor {

        private SerializedProperty? _targetAvatarProp;
        private SerializedProperty? _shopNameProp;      
        private SerializedProperty? _avatarNameProp;    
        private SerializedProperty? _variationNameProp; 
        private SerializedProperty? _designNameProp;
        private SerializedProperty? _materialNameProp;
        private SerializedProperty? _colorNameProp;
        private SerializedProperty? _nailShapeProp;
        private SerializedProperty? _useFootNailProp;

        private void OnEnable() {
            _targetAvatarProp = serializedObject.FindProperty("targetAvatar");
            _shopNameProp = serializedObject.FindProperty("shopName");
            _avatarNameProp = serializedObject.FindProperty("avatarName");
            _variationNameProp = serializedObject.FindProperty("variationName");
            _designNameProp = serializedObject.FindProperty("designName");
            _materialNameProp = serializedObject.FindProperty("materialName");
            _colorNameProp = serializedObject.FindProperty("colorName");
            _nailShapeProp = serializedObject.FindProperty("nailShape");
            _useFootNailProp = serializedObject.FindProperty("useFootNail");

            AutoDetectDesignFromFolder();
            AutoDetectAvatar(); 
        }

        private void AutoDetectAvatar() {
            MDNailAutoInstaller installer = (MDNailAutoInstaller)target;
            
            if (installer.targetAvatar == null) {
                VRCAvatarDescriptor descriptor = installer.GetComponentInParent<VRCAvatarDescriptor>();
                if (descriptor != null) {
                    serializedObject.Update();
                    _targetAvatarProp!.objectReferenceValue = descriptor;
                    serializedObject.ApplyModifiedProperties();
                    installer.targetAvatar = descriptor;
                }
            }

            if (installer.targetAvatar != null && string.IsNullOrEmpty(installer.shopName)) {
                AvatarMatching matching = new AvatarMatching(installer.targetAvatar);
                var matchResult = matching.Match();

                if (matchResult != null) {
                    (Shop shop, Entity.Avatar avatar, AvatarVariation variation) = matchResult.Value;
                    serializedObject.Update();
                    _shopNameProp!.stringValue = shop.ShopName;
                    _avatarNameProp!.stringValue = avatar.AvatarName;
                    _variationNameProp!.stringValue = variation.VariationName;
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private void AutoDetectDesignFromFolder() {
            MDNailAutoInstaller installer = (MDNailAutoInstaller)target;
            if (!string.IsNullOrEmpty(installer.designName)) return;

            string assetPath = "";
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(installer.gameObject);
            if (source != null) {
                assetPath = AssetDatabase.GetAssetPath(source);
            } else {
                assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(installer.gameObject);
            }

            if (string.IsNullOrEmpty(assetPath)) return;

            string directory = Path.GetDirectoryName(assetPath);
            string folderName = Path.GetFileName(directory);

            Match match = Regex.Match(folderName, @"【(.+)】");
            if (match.Success) {
                serializedObject.Update();
                _designNameProp!.stringValue = match.Groups[1].Value;
                serializedObject.ApplyModifiedProperties();
            }
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            MDNailAutoInstaller installer = (MDNailAutoInstaller)target;

            EditorGUILayout.LabelField("An-Labo Nail Installer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // --- 1. アバター選択 ---
            DrawAvatarSelector(installer);
            EditorGUILayout.Space();

            // --- 2. デザイン選択 ---
            DrawDesignSelector(installer);

            // --- 3. 詳細設定 ---
            DrawDetailSelectors(installer);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_useFootNailProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAvatarSelector(MDNailAutoInstaller installer) {
            EditorGUILayout.LabelField("Target Avatar", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_targetAvatarProp, GUIContent.none);

            if (installer.targetAvatar == null) {
                EditorGUILayout.HelpBox("アバターを指定してください", MessageType.Info);
                return;
            }

            // DBShopを使ってショップリストを取得
            using DBShop db = new();
            if (db.collection == null || db.collection.Count == 0) {
                EditorGUILayout.HelpBox("No shopDB", MessageType.Warning);
                return;
            }

            List<Shop> allShops = db.collection.ToList();

            // --- Shop Selection ---
            string currentShopName = _shopNameProp!.stringValue;
            int shopIndex = allShops.FindIndex(s => s.ShopName == currentShopName);
            
            if (shopIndex < 0) {
                shopIndex = 0;
                if(string.IsNullOrEmpty(currentShopName)) {
                     _shopNameProp.stringValue = allShops[0].ShopName;
                }
            }

            string[] shopNames = allShops.Select(s => s.ShopName).ToArray();
            int newShopIndex = EditorGUILayout.Popup("Shop", shopIndex, shopNames);

            Shop selectedShop = allShops[newShopIndex];
            
            if (newShopIndex != shopIndex || selectedShop.ShopName != currentShopName) {
                _shopNameProp.stringValue = selectedShop.ShopName;
                _avatarNameProp!.stringValue = "";
                _variationNameProp!.stringValue = "";
            }

            // --- Avatar Selection ---
            if (selectedShop.Avatars != null && selectedShop.Avatars.Count > 0) {
                List<Entity.Avatar> avatars = selectedShop.Avatars.Values.ToList();
                
                string currentAvatarName = _avatarNameProp!.stringValue;
                int avatarIndex = avatars.FindIndex(a => a.AvatarName == currentAvatarName);
                if (avatarIndex < 0) {
                    avatarIndex = 0;
                     if(string.IsNullOrEmpty(currentAvatarName)) {
                         _avatarNameProp.stringValue = avatars[0].AvatarName;
                     }
                }

                string[] avatarNames = avatars.Select(a => a.AvatarName).ToArray();
                int newAvatarIndex = EditorGUILayout.Popup("Avatar", avatarIndex, avatarNames);
                
                Entity.Avatar selectedAvatar = avatars[newAvatarIndex];
                if (newAvatarIndex != avatarIndex || selectedAvatar.AvatarName != currentAvatarName) {
                    _avatarNameProp.stringValue = selectedAvatar.AvatarName;
                    _variationNameProp!.stringValue = "";
                }

                // --- Variation Selection ---
                if (selectedAvatar.AvatarVariations != null && selectedAvatar.AvatarVariations.Count > 0) {
                    List<AvatarVariation> variations = selectedAvatar.AvatarVariations.Values.ToList();

                    string currentVarName = _variationNameProp!.stringValue;
                    int varIndex = variations.FindIndex(v => v.VariationName == currentVarName);
                    if (varIndex < 0) {
                        varIndex = 0;
                         if(string.IsNullOrEmpty(currentVarName)) {
                             _variationNameProp.stringValue = variations[0].VariationName;
                         }
                    }

                    string[] varNames = variations.Select(v => v.VariationName).ToArray();
                    int newVarIndex = EditorGUILayout.Popup("Variation", varIndex, varNames);
                    
                    if (newVarIndex != varIndex) {
                        _variationNameProp.stringValue = variations[newVarIndex].VariationName;
                    }
                } else {
                     if(!string.IsNullOrEmpty(_variationNameProp!.stringValue)) _variationNameProp.stringValue = "";
                     EditorGUILayout.LabelField("Variation", "Default / None");
                }
            } else {
                EditorGUILayout.HelpBox("No Avatar", MessageType.Warning);
            }
        }

        private void DrawDesignSelector(MDNailAutoInstaller installer) {
            using DBNailDesign db = new();
            List<string> allDesigns = db.collection.Select(d => d.DesignName).ToList();

            if (allDesigns.Count == 0) {
                EditorGUILayout.HelpBox("No nailDB", MessageType.Error);
                return;
            }

            int currentIndex = allDesigns.IndexOf(_designNameProp!.stringValue);
            if (currentIndex < 0) currentIndex = 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Design");
            int newIndex = EditorGUILayout.Popup(currentIndex, allDesigns.ToArray());
            EditorGUILayout.EndHorizontal();

            if (newIndex != currentIndex || string.IsNullOrEmpty(_designNameProp.stringValue)) {
                _designNameProp.stringValue = allDesigns[newIndex];
                _materialNameProp!.stringValue = "";
                _colorNameProp!.stringValue = "";
            }
        }

        private void DrawDetailSelectors(MDNailAutoInstaller installer) {
            string currentDesignName = _designNameProp!.stringValue;
            if (string.IsNullOrEmpty(currentDesignName)) return;

            using DBNailDesign db = new();
            NailDesign? design = db.FindNailDesignByDesignName(currentDesignName);

            if (design == null) return;

            // --- Shape Selector ---
            string[] shapes = new string[] { "Oval", "Point", "Square", "Round" };
            int shapeIndex = System.Array.IndexOf(shapes, _nailShapeProp!.stringValue);
            if (shapeIndex < 0) shapeIndex = 0;
            _nailShapeProp.stringValue = shapes[EditorGUILayout.Popup("Shape", shapeIndex, shapes)];

            // --- Material Selector ---
            List<string> materials = design.MaterialVariation?.Select(x => x.Value.MaterialName).ToList() ?? new List<string>();
            if (materials.Count > 0) {
                int matIndex = materials.IndexOf(_materialNameProp!.stringValue);
                if (matIndex < 0) {
                    matIndex = 0;
                    _materialNameProp.stringValue = materials[0];
                }
                int newMatIndex = EditorGUILayout.Popup("Material", matIndex, materials.ToArray());
                if (newMatIndex != matIndex) {
                    _materialNameProp.stringValue = materials[newMatIndex];
                }
            } else {
                if(!string.IsNullOrEmpty(_materialNameProp!.stringValue)) _materialNameProp.stringValue = "";
            }

            // --- Color Selector ---
            List<string> colors = design.ColorVariation?.Select(x => x.Value.ColorName).ToList() ?? new List<string>();
            if (colors.Count > 0) {
                int colIndex = colors.IndexOf(_colorNameProp!.stringValue);
                if (colIndex < 0) {
                    colIndex = 0;
                    _colorNameProp.stringValue = colors[0]; 
                }
                int newColIndex = EditorGUILayout.Popup("Color", colIndex, colors.ToArray());
                if (newColIndex != colIndex) {
                    _colorNameProp.stringValue = colors[newColIndex];
                }
            } else {
                 if(!string.IsNullOrEmpty(_colorNameProp!.stringValue)) _colorNameProp.stringValue = "";
                 EditorGUILayout.LabelField("Color", "No Options");
            }
        }
    }
}
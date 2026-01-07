using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Editor.Model;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Editor; 
using world.anlabo.mdnailtool.Runtime.Components;
using Avatar = world.anlabo.mdnailtool.Editor.Entity.Avatar;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Logic {
    
    [InitializeOnLoad]
    public static class MDNailAutoInstallerLogic {

        static MDNailAutoInstallerLogic() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredPlayMode) {
                RunAutoInstall();
            }
        }

        private static void RunAutoInstall() {
            var installers = UnityEngine.Object.FindObjectsOfType<MDNailAutoInstaller>();
            foreach (var installer in installers) {
                InstallNailFor(installer);
            }
        }

        private static void InstallNailFor(MDNailAutoInstaller installer) {
            if (installer == null) return;

            if (string.IsNullOrEmpty(installer.designName)) return;

            VRCAvatarDescriptor? descriptor = installer.targetAvatar;
            if (descriptor == null) descriptor = installer.GetComponentInParent<VRCAvatarDescriptor>();
            
            if (descriptor == null) return;

            try {
                AvatarMatching matching = new AvatarMatching(descriptor);
                var matchResult = matching.Match();

                if (matchResult == null) {
                    Debug.LogWarning($"[MDNail Installer] アバターに対応する設定が見つかりません: {descriptor.gameObject.name}");
                    return;
                }

                (Shop shop, Avatar avatar, AvatarVariation variation) = matchResult.Value;

                GameObject? avatarNailPrefab = FindAvatarSpecificPrefab(avatar, variation, installer.nailShape);

                if (avatarNailPrefab == null) {
                    Debug.LogError($"[MDNail Installer] アバター '{avatar.AvatarName}' ({variation.VariationName}) 用のネイルPrefabが見つかりませんでした。");
                    return;
                }

                INailProcessor nailProcessor = INailProcessor.CreateNailDesign(installer.designName);
                
                var singleConfig = (nailProcessor, installer.materialName, installer.colorName);
                var designConfig = Enumerable.Repeat(singleConfig, 12).ToArray();

                NailSetupProcessor processor = new NailSetupProcessor(
                    descriptor, 
                    variation, 
                    avatarNailPrefab, 
                    designConfig, 
                    installer.nailShape
                ) {
                    AvatarName = descriptor.gameObject.name,
                    UseFootNail = installer.useFootNail,
                    RemoveCurrentNail = true,
                    GenerateMaterial = true,
                    Backup = false,
                    ForModularAvatar = installer.useModularAvatar,
                    OverrideMesh = null 
                };

                processor.Process();
                Debug.Log($"[MDNail Installer] Auto Setup: {installer.designName} on {descriptor.gameObject.name}");

                UnityEngine.Object.DestroyImmediate(installer.gameObject);

            } catch (Exception e) {
                Debug.LogError($"[MDNail Installer] Setup Failed: {e.Message}\n{e.StackTrace}");
            }
        }

        private static GameObject? FindAvatarSpecificPrefab(Avatar avatar, AvatarVariation variation, string shape) {

            string[] searchQueries = new string[] {
                variation.VariationName,
                avatar.AvatarName,
            };

            foreach (string query in searchQueries) {
                if(string.IsNullOrEmpty(query)) continue;

                string[] guids = AssetDatabase.FindAssets($"t:Prefab {query}");

                foreach (string guid in guids) {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    
                    if (!path.Contains("/Resource/Nail/Prefab/")) continue;

                    string fileName = Path.GetFileName(path);
                    
                    if (fileName.Contains($"[{shape}]") && fileName.Contains(query, StringComparison.OrdinalIgnoreCase)) {
                        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    }
                }
            }

            foreach (string query in searchQueries) {
                if(string.IsNullOrEmpty(query)) continue;
                string[] guids = AssetDatabase.FindAssets($"t:Prefab {query}");
                foreach (string guid in guids) {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.Contains("/Resource/Nail/Prefab/")) continue;
                    
                    if (path.Contains(query, StringComparison.OrdinalIgnoreCase)) {
                        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    }
                }
            }

            return null;
        }
    }
}
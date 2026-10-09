#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor.Entity;
using world.anlabo.mdnailtool.Runtime;

namespace world.anlabo.mdnailtool.Editor {
    public static partial class NailSetupUtil {
        public static string[] FingerWeightKeys => MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST.Concat(MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST).Concat(MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST).ToArray();

        public static SkinnedMeshRenderer? ResolveFingerWeightSource(GameObject avatar, IEnumerable<Transform?> nailObjects, IEnumerable<string> bodyNames, bool isHand, GameObject? excludedRoot = null) {
            var nails = nailObjects.Where(t => t != null && t.parent != null).Select(t => t!).ToArray();
            var chains = nails.Select(t => new[] { t.parent, t.parent.parent, t.parent.parent?.parent }.Where(b => b != null).ToArray()).ToArray();
            var targetBones = chains.SelectMany(c => c).ToHashSet();
            var declared = bodyNames.ToHashSet();
            return avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r.sharedMesh != null && r.sharedMesh.isReadable && r.bones.Length > 0 && !nails.Contains(r.transform)
                    && (excludedRoot == null || !r.transform.IsChildOf(excludedRoot.transform)) && r.GetComponentInParent<MDNailObjectMarker>(true) == null)
                .Select(r => {
                    var bones = r.bones;
                    var weighted = new HashSet<Transform>();
                    foreach (var w in r.sharedMesh.boneWeights) {
                        if (w.weight0 > 0 && w.boneIndex0 >= 0 && w.boneIndex0 < bones.Length) weighted.Add(bones[w.boneIndex0]);
                        if (w.weight1 > 0 && w.boneIndex1 >= 0 && w.boneIndex1 < bones.Length) weighted.Add(bones[w.boneIndex1]);
                        if (w.weight2 > 0 && w.boneIndex2 >= 0 && w.boneIndex2 < bones.Length) weighted.Add(bones[w.boneIndex2]);
                        if (w.weight3 > 0 && w.boneIndex3 >= 0 && w.boneIndex3 < bones.Length) weighted.Add(bones[w.boneIndex3]);
                    }
                    return new { renderer = r, count = weighted.Count(b => targetBones.Contains(b)), covers = chains.Length > 0 && chains.All(chain => bones.Contains(chain[0]) && chain.Any(b => weighted.Contains(b))) };
                }).Where(x => x.covers && x.count > 0).OrderByDescending(x => x.count)
                .ThenByDescending(x => declared.Contains(x.renderer.name)).ThenByDescending(x => x.renderer.name.IndexOf(isHand ? "hand" : "foot", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenByDescending(x => x.renderer.sharedMesh.vertexCount).Select(x => x.renderer).FirstOrDefault();
        }

        // Read-only diagnostics. All objects and meshes are temporary; no asset save, Undo or Process call.
        public static List<FingerWeightAssessment> DiagnoseFingerWeights(GameObject source, NailPrefabNodeData[]? handNodes, NailPrefabNodeData[]? footNodes, IReadOnlyDictionary<string, string>? mapping, IEnumerable<string> bodyNames) {
            var result = new List<FingerWeightAssessment>();
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject? clone = null;
            var temporaryMeshes = new List<Mesh>();
            var meshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().ToHashSet();
            using var temporaryScope = NailDesigns.NailPrefabBuilder.BeginTemporaryScope();
            try {
                clone = UnityEngine.Object.Instantiate(source);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone, scene);
                clone.hideFlags = HideFlags.HideAndDontSave;
                var descriptor = clone.GetComponent<VRCAvatarDescriptor>() ?? clone.AddComponent<VRCAvatarDescriptor>();
                var bones = NailSetupProcessor.GetTargetBoneDictionary(descriptor, mapping);
                var roots = handNodes ?? Array.Empty<NailPrefabNodeData>();
                if (roots.Any(n => (n.Name ?? "").StartsWith("[Natural]", StringComparison.OrdinalIgnoreCase))) roots = roots.Where(n => (n.Name ?? "").StartsWith("[Natural]", StringComparison.OrdinalIgnoreCase)).ToArray();
                else if (roots.Any(n => System.Text.RegularExpressions.Regex.IsMatch(n.Name ?? "", @"^\[[A-Za-z]+\]"))) roots = Array.Empty<NailPrefabNodeData>();
                var prefab = NailDesigns.NailPrefabBuilder.BuildTemporaryFromNodes(roots, "WeightDiagnosis", "Natural");
                prefab.transform.SetParent(clone.transform, false);
                if (footNodes != null && footNodes.Length > 0 && !prefab.GetComponentsInChildren<Transform>(true).Any(t => FingerWeightKeys.Skip(10).Contains(FingerKeyFromName(t.name)))) {
                    var foot = NailDesigns.NailPrefabBuilder.BuildTemporaryFromNodes(footNodes, "FootDiagnosis", "Natural");
                    foot.transform.SetParent(prefab.transform, false);
                }
                var keys = FingerWeightKeys;
                var boneKeys = MDNailToolDefines.TARGET_HANDS_BONE_NAME_LIST.Concat(MDNailToolDefines.LEFT_FOOT_FINGER_BONE_NAME_LIST.Take(5)).Concat(MDNailToolDefines.RIGHT_FOOT_FINGER_BONE_NAME_LIST.Take(5)).ToArray();
                var all = prefab.GetComponentsInChildren<Transform>(true);
                var matches = keys.Select(k => all.Where(t => FingerKeyFromName(t.name) == k).ToArray()).ToArray();
                var nails = matches.Select(m => m.Length == 1 ? m[0] : null).ToArray();
                for (int i = 0; i < nails.Length; i++) {
                    var nail = nails[i];
                    if (nail == null || nail.GetComponent<SkinnedMeshRenderer>()?.sharedMesh == null) { result.Add(new FingerWeightAssessment { FingerKey = keys[i], Reason = matches[i].Length > 1 ? "Naturalの配置に同じ指が複数あり特定できません" : "Naturalの爪位置・メッシュが未登録です" }); nails[i] = null; continue; }
                    var bone = bones.TryGetValue(boneKeys[i], out var target) ? target : null;
                    if (mapping != null && mapping.TryGetValue(boneKeys[i], out var manual) && !string.IsNullOrWhiteSpace(manual) && clone.transform.Find(manual) == null) bone = null;
                    if (bone == null) { result.Add(new FingerWeightAssessment { FingerKey = keys[i], Reason = "対応する指のボーンを特定できません" }); nails[i] = null; continue; }
                    nail.SetParent(bone, true);
                }
                for (int zone = 0; zone < 2; zone++) {
                    var zoneNails = nails.Skip(zone * 10).Take(10).ToArray();
                    var body = ResolveFingerWeightSource(clone, zoneNails, bodyNames, zone == 0, prefab);
                    var combined = BakeAndCombineNailMeshes(zoneNails, prefab, zone == 0 ? "HandDiagnosis" : "FootDiagnosis", "", bodySmr: body, transferBodyWeightsByNail: Enumerable.Repeat(true, 10).ToArray(), weightTransferModesByNail: new int[10], assessments: result, inMemoryOnly: true);
                    if (combined != null) temporaryMeshes.Add(combined.GetComponent<SkinnedMeshRenderer>().sharedMesh);
                }
                foreach (var key in keys.Where(k => result.All(r => r.FingerKey != k))) result.Add(new FingerWeightAssessment { FingerKey = key, Reason = "指の必要性を判定できません" });
                return result.OrderBy(r => Array.IndexOf(keys, r.FingerKey)).ToList();
            } finally {
                foreach (var mesh in temporaryMeshes) if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                foreach (var mesh in Resources.FindObjectsOfTypeAll<Mesh>().Where(m => !meshesBefore.Contains(m) && (m.name == "HandDiagnosis" || m.name == "FootDiagnosis") && !UnityEditor.EditorUtility.IsPersistent(m))) UnityEngine.Object.DestroyImmediate(mesh);
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}

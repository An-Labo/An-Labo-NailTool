using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using world.anlabo.mdnailtool.Editor;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Runtime;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Window.Controllers
{
    internal sealed class MDNailScenePreviewController
    {
        private readonly string _scenePreviewName;
        private GameObject? _scenePreviewObject;
        private Transform? _scenePreviewRoot;
        private GameObject? _lastPrefabSource;

        private readonly Dictionary<Renderer, bool> _originalRendererEnabled = new();
        private bool _isOriginalHidden = false;

        // Armature補正前のネイルローカルトランスフォームを保存
        private readonly Dictionary<string, (Vector3 localPos, Quaternion localRot, Vector3 localScale)> _originalNailLocalTransforms = new();

        public MDNailScenePreviewController(string scenePreviewName)
        {
            _scenePreviewName = scenePreviewName;
        }

        public void Cleanup(VRCAvatarDescriptor? avatar)
        {
            RestoreOriginalNails();

            if (_scenePreviewObject != null)
            {
                Object.DestroyImmediate(_scenePreviewObject);
                _scenePreviewObject = null;
                _scenePreviewRoot = null;
                _originalNailLocalTransforms.Clear();
            }
            _lastPrefabSource = null;

            if (avatar == null) return;

            var existing = avatar.transform.Find(_scenePreviewName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        public void Update(
            VRCAvatarDescriptor? avatar,
            GameObject? prefab,
            Mesh?[] overrideMeshes,
            string nailShapeName,
            bool isHandActive,
            bool isFootActive,
            (INailProcessor, string, string)[] designAndVariationNames,
            Material? overrideMaterial = null,
            bool enableAdditionalMaterials = true,
            IEnumerable<Material>?[]? perFingerAdditionalMaterials = null,
            IEnumerable<Transform>?[]? perFingerAdditionalObjects = null,
            bool armatureScaleCompensation = false
        )
        {
            if (avatar == null) return;

            // シェイプ切替などで渡された Prefab が変わったら作り直す (NaturalのScale固定防止)
            if (_scenePreviewObject != null && prefab != null && _lastPrefabSource != prefab)
            {
                Object.DestroyImmediate(_scenePreviewObject);
                _scenePreviewObject = null;
                _scenePreviewRoot = null;
                _originalNailLocalTransforms.Clear();
            }

            if (_scenePreviewObject == null)
            {
                var existing = avatar.transform.Find(_scenePreviewName);
                if (existing != null)
                {
                    // 既存オブジェクトの生成元 Prefab 不明なので _lastPrefabSource は更新しない
                    // 次回 Update で必ず prefab 不一致扱い→作り直しが走る
                    _scenePreviewObject = existing.gameObject;
                }
                else
                {
                    if (prefab == null) return;

                    _scenePreviewObject = Object.Instantiate(prefab);
                    _scenePreviewObject.name = _scenePreviewName;
                    _scenePreviewObject.transform.SetParent(avatar.transform, false);
                    _scenePreviewObject.hideFlags = HideFlags.DontSave;
                    _lastPrefabSource = prefab;
                }

                _scenePreviewRoot = _scenePreviewObject.transform;
            }

            HideOriginalNails(avatar);

            if (_scenePreviewObject != null)
            {
                _scenePreviewObject.SetActive(true);
            }

            if (_scenePreviewObject == null) return;

            var allTransforms = _scenePreviewObject.GetComponentsInChildren<Transform>(true);
            Transform? FindByName(string name) => allTransforms.FirstOrDefault(t => t.name.Contains(name));

            var hands = MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST.Select(FindByName).ToArray();
            foreach (var t in hands) if (t != null) t.gameObject.SetActive(isHandActive);

            var feet = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST
                .Concat(MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST)
                .Select(FindByName).ToArray();
            foreach (var t in feet) if (t != null) t.gameObject.SetActive(isFootActive);

            // 初回のみローカルトランスフォームを保存（Armature補正リセット用）
            SaveNailLocalTransforms(hands);
            SaveNailLocalTransforms(feet);

            if (isHandActive && overrideMeshes.Length > 0)
            {
                NailSetupUtil.ReplaceHandsNailMesh(hands, overrideMeshes);
            }

            var pLeftFeet = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST.Select(FindByName);
            var pRightFeet = MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST.Select(FindByName);

            NailSetupUtil.ReplaceNailMaterial(
                hands,
                pLeftFeet,
                pRightFeet,
                designAndVariationNames,
                nailShapeName,
                true,
                true,
                overrideMaterial,
                enableAdditionalMaterials,
                perFingerAdditionalMaterials
            );

            // 追加オブジェクトの適用（手 + 足）
            {
                // 手の既存子をクリーンアップ
                foreach (var hand in hands)
                {
                    if (hand == null) continue;
                    foreach (Transform child in hand.Cast<Transform>().ToArray())
                        Object.DestroyImmediate(child.gameObject);
                }
                // 足の既存子をクリーンアップ
                foreach (var foot in feet)
                {
                    if (foot == null) continue;
                    foreach (Transform child in foot.Cast<Transform>().ToArray())
                        Object.DestroyImmediate(child.gameObject);
                }

                // perFingerAdditionalObjects[0..9]=手, [10..19]=足
                if (isHandActive)
                    NailSetupUtil.AttachAdditionalObjects(hands, designAndVariationNames, nailShapeName, true, perFingerAdditionalObjects);

                if (isFootActive && perFingerAdditionalObjects != null)
                {
                    // 足の追加オブジェクト 10-19 を feet に親付け
                    for (int fi = 0; fi < feet.Length && fi + 10 < perFingerAdditionalObjects.Length; fi++)
                    {
                        var footTransform = feet[fi];
                        var footObjects = perFingerAdditionalObjects[fi + 10];
                        if (footObjects == null) continue;
                        // 親付け先がない場合は Instantiate 済み孤児 GO を Destroy する (Scene 残留防止).
                        if (footTransform == null)
                        {
                            foreach (Transform obj in footObjects)
                            {
                                if (obj != null) Object.DestroyImmediate(obj.gameObject);
                            }
                            continue;
                        }
                        foreach (Transform obj in footObjects)
                            obj.SetParent(footTransform, false);
                    }
                }
            }

            // ---- ネイルトランスフォームをリセット（Armature補正の累積防止）----
            RestoreNailLocalTransforms(hands);
            RestoreNailLocalTransforms(feet);

            // ---- Armature補正の適用 ----
            if (armatureScaleCompensation && avatar != null)
            {
                ApplyArmatureCompensation(avatar, hands, feet, isHandActive, isFootActive);
            }
        }

        /// <summary>ネイルのローカル座標を初期値として保存（未保存の場合のみ）</summary>
        private void SaveNailLocalTransforms(Transform?[] nails)
        {
            foreach (var nail in nails)
            {
                if (nail == null) continue;
                string key = nail.name;
                if (!_originalNailLocalTransforms.ContainsKey(key))
                    _originalNailLocalTransforms[key] = (nail.localPosition, nail.localRotation, nail.localScale);
            }
        }

        /// <summary>保存したローカル座標に復元</summary>
        private void RestoreNailLocalTransforms(Transform?[] nails)
        {
            foreach (var nail in nails)
            {
                if (nail == null) continue;
                if (_originalNailLocalTransforms.TryGetValue(nail.name, out var orig))
                {
                    nail.localPosition = orig.localPos;
                    nail.localRotation = orig.localRot;
                    nail.localScale = orig.localScale;
                }
            }
        }

        private void ApplyArmatureCompensation(
            VRCAvatarDescriptor avatar,
            Transform?[] hands,
            Transform?[] feet,
            bool isHandActive,
            bool isFootActive)
        {
            var targetBoneDictionary = NailSetupProcessor.GetTargetBoneDictionary(avatar, null);

            var allNails = new List<Transform?>();
            var allBoneIndices = new List<int>();

            if (isHandActive)
            {
                int ci = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
                foreach (var nail in hands)
                {
                    ci++;
                    allNails.Add(nail);
                    allBoneIndices.Add(ci);
                }
            }

            if (isFootActive)
            {
                var leftFeetNames = MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST;
                int lci = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
                for (int i = 0; i < leftFeetNames.Count && i < feet.Length; i++)
                {
                    lci++;
                    allNails.Add(feet[i]);
                    allBoneIndices.Add(lci);
                }
                int rci = (int)MDNailToolDefines.TargetFingerAndToe.RightFootThumb - 1;
                for (int i = 0; i < MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST.Count && i + leftFeetNames.Count < feet.Length; i++)
                {
                    rci++;
                    allNails.Add(feet[i + leftFeetNames.Count]);
                    allBoneIndices.Add(rci);
                }
            }

            if (allNails.Count == 0) return;

            var corrections = NailSetupProcessor.ComputeScaleCompensatedTransforms(
                avatar, targetBoneDictionary,
                allNails.ToArray(), allBoneIndices.ToArray());

            if (corrections == null || corrections.Count == 0) return;

            foreach (var kv in corrections)
            {
                Transform nail = kv.Key;
                nail.position = kv.Value.position;
                nail.rotation = kv.Value.rotation;
                nail.localScale = Vector3.Scale(nail.localScale, kv.Value.scaleRatio);
            }
        }

        private IEnumerable<Renderer> EnumerateOriginalNailRenderers(VRCAvatarDescriptor avatar)
        {
            bool IsUnderPreviewRoot(Transform t)
            {
                if (_scenePreviewRoot == null) return false;
                return t == _scenePreviewRoot || t.IsChildOf(_scenePreviewRoot);
            }

            foreach (var marker in avatar.GetComponentsInChildren<MDNailObjectMarker>(true))
            {
                if (IsUnderPreviewRoot(marker.transform)) continue;

                foreach (var r in marker.GetComponentsInChildren<Renderer>(true))
                {
                    if (IsUnderPreviewRoot(r.transform)) continue;
                    yield return r;
                }
            }

            var avatarAll = avatar.GetComponentsInChildren<Transform>(true);
            Transform? FindInAvatarByContains(string name)
                => avatarAll.FirstOrDefault(t => t.name.Contains(name) && !IsUnderPreviewRoot(t));

            IEnumerable<string> names =
                MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST
                .Concat(MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST)
                .Concat(MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST);

            foreach (var n in names)
            {
                var t = FindInAvatarByContains(n);
                if (t == null) continue;

                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (IsUnderPreviewRoot(r.transform)) continue;
                    yield return r;
                }
            }
        }

        private void HideOriginalNails(VRCAvatarDescriptor avatar)
        {
            foreach (var r in EnumerateOriginalNailRenderers(avatar))
            {
                if (!_originalRendererEnabled.ContainsKey(r))
                    _originalRendererEnabled[r] = r.enabled;
                r.enabled = false;
            }

            _isOriginalHidden = true;
        }

        private void RestoreOriginalNails()
        {
            if (!_isOriginalHidden) return;

            foreach (var kvp in _originalRendererEnabled)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.enabled = kvp.Value;
                }
            }

            _originalRendererEnabled.Clear();
            _isOriginalHidden = false;
        }

        /// <summary>
        /// A-2 fix: Apply 直前に呼び出して, Hide 中の元 Renderer を強制的に enabled=true に戻す.
        /// プレビュー Hide 状態のまま Apply -> Cleanup の流れに入ると元 Renderer が
        /// false のままシーンに保存される事例があるため, Cleanup より先に強制復元する保険.
        /// </summary>
        public void ForceRestoreAllRenderers()
        {
            foreach (var kvp in _originalRendererEnabled)
            {
                if (kvp.Key != null)
                    kvp.Key.enabled = true;
            }
            _originalRendererEnabled.Clear();
            _isOriginalHidden = false;
        }

        public void SetScenePreviewActive(VRCAvatarDescriptor avatar, bool active)
        {
            if (_scenePreviewObject != null)
            {
                _scenePreviewObject.SetActive(active);
            }

            if (active) HideOriginalNails(avatar);
            else RestoreOriginalNails();
        }



    }
}

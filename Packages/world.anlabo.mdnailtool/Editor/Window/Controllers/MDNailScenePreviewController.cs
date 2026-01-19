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

        private readonly Dictionary<Renderer, bool> _originalRendererEnabled = new();
        private bool _isOriginalHidden = false;

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
            }

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
            Material? overrideMaterial = null
        )
        {
            if (avatar == null) return;

            if (_scenePreviewObject == null)
            {
                var existing = avatar.transform.Find(_scenePreviewName);
                if (existing != null)
                {
                    _scenePreviewObject = existing.gameObject;
                }
                else
                {
                    if (prefab == null) return;

                    _scenePreviewObject = Object.Instantiate(prefab);
                    _scenePreviewObject.name = _scenePreviewName;
                    _scenePreviewObject.transform.SetParent(avatar.transform, false);
                    _scenePreviewObject.hideFlags = HideFlags.DontSave;
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
                overrideMaterial
            );
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
            if (_isOriginalHidden) return;

            _originalRendererEnabled.Clear();

            foreach (var r in EnumerateOriginalNailRenderers(avatar))
            {
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

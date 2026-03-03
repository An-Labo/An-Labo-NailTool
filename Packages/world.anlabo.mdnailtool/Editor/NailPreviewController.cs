using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Editor.VisualElements;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public class NailPreviewController {
		private readonly NailPreview _nailPreview;

		public NailPreviewController(NailPreview nailPreview) {
			this._nailPreview = nailPreview;
		}

		public void ChangeNailShape(Mesh?[] overrideMesh) {
			if (this._nailPreview.NailObj == null) return;
			Transform?[] handsNailObjects = GetHandsNailObjectList(this._nailPreview.NailObj);

			if (overrideMesh is { Length: > 0 }) {
				NailSetupUtil.ReplaceHandsNailMesh(handsNailObjects, overrideMesh);
			}
		}

		public void ChangeFootNailMesh(string nailShapeName) {
			if (this._nailPreview.NailObj == null) return;
			Transform?[] leftFootNailObjects = GetLeftFootNailObjectList(this._nailPreview.NailObj);
			Transform?[] rightFootNailObjects = GetRightFootNailObjectList(this._nailPreview.NailObj);
			NailSetupUtil.ReplaceFootNailMesh(leftFootNailObjects, rightFootNailObjects, nailShapeName);
		}

		public void ChangeNailMaterial((INailProcessor, string, string)[] designAndVariationNames, string nailShapeName, Material? overrideMaterial = null,
			bool enableAdditionalMaterials = true, IEnumerable<Material>?[]? perFingerAdditionalMaterials = null) {
            if (this._nailPreview.NailObj == null) return;
            Transform?[] handsNailObjects = GetHandsNailObjectList(this._nailPreview.NailObj);
            IEnumerable<Transform?> leftFootNailObjects = GetLeftFootNailObjectList(this._nailPreview.NailObj);
            IEnumerable<Transform?> rightFootNailObjects = GetRightFootNailObjectList(this._nailPreview.NailObj);

            NailSetupUtil.ReplaceNailMaterial(handsNailObjects, leftFootNailObjects, rightFootNailObjects, designAndVariationNames, nailShapeName, true, true, overrideMaterial,
				enableAdditionalMaterials, perFingerAdditionalMaterials);
        }

		public void ChangeAdditionalObjects((INailProcessor, string, string)[] designAndVariationNames, string nailShapeName,
			IEnumerable<Transform>?[]? perFingerAdditionalObjects = null) {
			if (this._nailPreview.NailObj == null) return;
			Transform?[] handsNailObjects = GetHandsNailObjectList(this._nailPreview.NailObj);
			Transform?[] leftFootNailObjects = GetLeftFootNailObjectList(this._nailPreview.NailObj);
			Transform?[] rightFootNailObjects = GetRightFootNailObjectList(this._nailPreview.NailObj);
			Transform?[] feetNailObjects = leftFootNailObjects.Concat(rightFootNailObjects).ToArray();

			// 手の既存子をクリーンアップ
			foreach (Transform? handsNailObject in handsNailObjects) {
				if (handsNailObject == null) continue;
				foreach (Transform child in handsNailObject.Cast<Transform>().ToArray()) {
					Object.DestroyImmediate(child.gameObject);
				}
			}
			// 足の既存子をクリーンアップ
			foreach (Transform? footNailObject in feetNailObjects) {
				if (footNailObject == null) continue;
				foreach (Transform child in footNailObject.Cast<Transform>().ToArray()) {
					Object.DestroyImmediate(child.gameObject);
				}
			}

			// 手の追加オブジェクト（0-9）
			NailSetupUtil.AttachAdditionalObjects(handsNailObjects, designAndVariationNames, nailShapeName, true, perFingerAdditionalObjects);

			// 足の追加オブジェクト（10-19）
			if (perFingerAdditionalObjects != null) {
				for (int fi = 0; fi < feetNailObjects.Length && fi + 10 < perFingerAdditionalObjects.Length; fi++) {
					var footTransform = feetNailObjects[fi];
					var footObjects = perFingerAdditionalObjects[fi + 10];
					if (footTransform == null || footObjects == null) continue;
					foreach (Transform obj in footObjects)
						obj.SetParent(footTransform, false);
				}
			}
		}

		public void CleanupAdditionalObjects() {
			if (this._nailPreview.NailObj == null) return;
			Transform?[] handsNailObjects = GetHandsNailObjectList(this._nailPreview.NailObj);
			foreach (Transform? hand in handsNailObjects) {
				if (hand == null) continue;
				foreach (Transform child in hand.Cast<Transform>().ToArray()) {
					Object.DestroyImmediate(child.gameObject);
				}
			}
			Transform?[] leftFootNailObjects = GetLeftFootNailObjectList(this._nailPreview.NailObj);
			Transform?[] rightFootNailObjects = GetRightFootNailObjectList(this._nailPreview.NailObj);
			foreach (Transform? foot in leftFootNailObjects.Concat(rightFootNailObjects)) {
				if (foot == null) continue;
				foreach (Transform child in foot.Cast<Transform>().ToArray()) {
					Object.DestroyImmediate(child.gameObject);
				}
			}
		}

		public void UpdateVisibility(bool isHandActive, bool isFootActive) {
			if (this._nailPreview.NailObj == null) return;

			Transform?[] handsNailObjects = GetHandsNailObjectList(this._nailPreview.NailObj);
			foreach (var transform in handsNailObjects) {
				transform?.gameObject.SetActive(isHandActive);
			}

			Transform?[] leftFootNailObjects = GetLeftFootNailObjectList(this._nailPreview.NailObj);
			foreach (var transform in leftFootNailObjects) {
				transform?.gameObject.SetActive(isFootActive);
			}

			Transform?[] rightFootNailObjects = GetRightFootNailObjectList(this._nailPreview.NailObj);
			foreach (var transform in rightFootNailObjects) {
				transform?.gameObject.SetActive(isFootActive);
			}
		}


		private static Transform?[] GetHandsNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST.Select(name => nailPrefabObject.transform.Find(name)).ToArray();
		}

		private static Transform?[] GetLeftFootNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST.Select(name => nailPrefabObject.transform.Find(name)).ToArray();
		}

		private static Transform?[] GetRightFootNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST.Select(name => nailPrefabObject.transform.Find(name)).ToArray();
		}
	}
}
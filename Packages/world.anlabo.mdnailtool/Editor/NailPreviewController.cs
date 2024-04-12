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

		public void ChangeNailMaterial((INailProcessor, string, string)[] designAndVariationNames, string nailShapeName) {
			if (this._nailPreview.NailObj == null) return;
			Transform?[] handsNailObjects = GetHandsNailObjectList(this._nailPreview.NailObj);
			IEnumerable<Transform?> leftFootNailObjects = GetLeftFootNailObjectList(this._nailPreview.NailObj);
			IEnumerable<Transform?> rightFootNailObjects = GetRightFootNailObjectList(this._nailPreview.NailObj);

			NailSetupUtil.ReplaceNailMaterial(handsNailObjects, leftFootNailObjects, rightFootNailObjects, designAndVariationNames, nailShapeName, true, true);
		}

		public void ChangeAdditionalObjects((INailProcessor, string, string)[] designAndVariationNames, string nailShapeName) {
			if (this._nailPreview.NailObj == null) return;
			Transform?[] handsNailObjects = GetHandsNailObjectList(this._nailPreview.NailObj);

			foreach (Transform? handsNailObject in handsNailObjects) {
				if (handsNailObject == null) continue;
				foreach (Transform child in handsNailObject.Cast<Transform>().ToArray()) {
					Object.DestroyImmediate(child.gameObject);
				}
			}
			
			NailSetupUtil.AttachAdditionalObjects(handsNailObjects, designAndVariationNames, nailShapeName, true);
		}


		private static Transform?[] GetHandsNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.HANDS_NAIL_OBJECT_NAME_LIST.Select(name => nailPrefabObject.transform.Find(name)).ToArray();
		}

		private static IEnumerable<Transform?> GetLeftFootNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.LEFT_FOOT_NAIL_OBJECT_NAME_LIST.Select(name => nailPrefabObject.transform.Find(name)).ToArray();
		}

		private static IEnumerable<Transform?> GetRightFootNailObjectList(GameObject nailPrefabObject) {
			return MDNailToolDefines.RIGHT_FOOT_NAIL_OBJECT_NAME_LIST.Select(name => nailPrefabObject.transform.Find(name)).ToArray();
		}
	}
}
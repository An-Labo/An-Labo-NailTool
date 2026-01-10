using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.NailDesigns;
using world.anlabo.mdnailtool.Editor.VisualElements;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	/// <summary>
	/// ネイルプレビューの表示・操作を制御するコントローラークラスです。
	/// </summary>
	public class NailPreviewController {
		private readonly NailPreview _nailPreview;

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="nailPreview">操作対象のプレビュー要素</param>
		public NailPreviewController(NailPreview nailPreview) {
			this._nailPreview = nailPreview;
		}

		/// <summary>
		/// ネイルの形状（メッシュ）を変更します。
		/// </summary>
		public void ChangeNailShape(Mesh?[] overrideMesh) {
			if (this._nailPreview.NailObj == null) return;
			Transform?[] handsNailObjects = GetHandsNailObjectList(this._nailPreview.NailObj);

			if (overrideMesh is { Length: > 0 }) {
				NailSetupUtil.ReplaceHandsNailMesh(handsNailObjects, overrideMesh);
			}
		}

		/// <summary>
		/// フットネイルの形状を変更します。
		/// </summary>
		public void ChangeFootNailMesh(string nailShapeName) {
			if (this._nailPreview.NailObj == null) return;
			Transform?[] leftFootNailObjects = GetLeftFootNailObjectList(this._nailPreview.NailObj);
			Transform?[] rightFootNailObjects = GetRightFootNailObjectList(this._nailPreview.NailObj);
			NailSetupUtil.ReplaceFootNailMesh(leftFootNailObjects, rightFootNailObjects, nailShapeName);
		}

		/// <summary>
		/// ネイルのマテリアル（デザイン）を変更します。
		/// </summary>
		public void ChangeNailMaterial((INailProcessor, string, string)[] designAndVariationNames, string nailShapeName) {
			if (this._nailPreview.NailObj == null) return;
			Transform?[] handsNailObjects = GetHandsNailObjectList(this._nailPreview.NailObj);
			IEnumerable<Transform?> leftFootNailObjects = GetLeftFootNailObjectList(this._nailPreview.NailObj);
			IEnumerable<Transform?> rightFootNailObjects = GetRightFootNailObjectList(this._nailPreview.NailObj);

			NailSetupUtil.ReplaceNailMaterial(handsNailObjects, leftFootNailObjects, rightFootNailObjects, designAndVariationNames, nailShapeName, true, true);
		}

		/// <summary>
		/// 追加オブジェクト（パーツなど）を適用します。
		/// </summary>
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

		/// <summary>
		/// ハンド・フットネイルの表示/非表示状態を更新します。
		/// UIのチェックボックスと連動してプレビューのメッシュを表示・非表示にします。
		/// </summary>
		/// <param name="isHandActive">ハンドネイルを表示するか</param>
		/// <param name="isFootActive">フットネイルを表示するか</param>
		public void UpdateVisibility(bool isHandActive, bool isFootActive) {
			if (this._nailPreview.NailObj == null) return;

			// ハンドの表示切替
			Transform?[] handsNailObjects = GetHandsNailObjectList(this._nailPreview.NailObj);
			foreach (var transform in handsNailObjects) {
				transform?.gameObject.SetActive(isHandActive);
			}

			// フット（左）の表示切替
			Transform?[] leftFootNailObjects = GetLeftFootNailObjectList(this._nailPreview.NailObj);
			foreach (var transform in leftFootNailObjects) {
				transform?.gameObject.SetActive(isFootActive);
			}

			// フット（右）の表示切替
			Transform?[] rightFootNailObjects = GetRightFootNailObjectList(this._nailPreview.NailObj);
			foreach (var transform in rightFootNailObjects) {
				transform?.gameObject.SetActive(isFootActive);
			}
		}

		// --- Helper Methods ---

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
using System.Collections.Generic;
using UnityEngine;
using world.anlabo.mdnailtool.Editor.Model;
using Object = UnityEngine.Object;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public partial class NailSetupProcessor {
		private void SetupDirect(
			GameObject nailPrefabObject,
			Dictionary<string, Transform?> targetBoneDictionary,
			Transform?[] handsNailObjects,
			Transform?[] leftFootNailObjects,
			Transform?[] rightFootNailObjects,
			Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 desiredLossyScale)>? corrections) {
				int index = (int)MDNailToolDefines.TargetFingerAndToe.LeftThumb - 1;
				foreach (Transform? nailObject in handsNailObjects) {
					index++;
					if (nailObject == null) continue;
					Transform? target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
					if (target == null) {
						ToolConsole.Error("NailSetup", $"Not found target bone : {MDNailToolDefines.TARGET_BONE_NAME_LIST[index]}");
						continue;
					}

					Vector3? desired = null;
					if (corrections != null && corrections.TryGetValue(nailObject, out var c)) {
						nailObject.position = c.position;
						nailObject.rotation = c.rotation;
						desired = c.desiredLossyScale;
					}
					nailObject.SetParent(target, true);
					if (desired.HasValue) EnforceLossyScale(nailObject, desired.Value);
				}

				if (this.UseFootNail) {
					index = (int)MDNailToolDefines.TargetFingerAndToe.LeftFootThumb - 1;
					foreach (Transform? nailObject in leftFootNailObjects) {
						index++;
						if (nailObject == null) continue;
						Transform? target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
						if (target == null) {
							ToolConsole.Error("NailSetup", $"Not found target bone : {MDNailToolDefines.TARGET_BONE_NAME_LIST[index]}");
							continue;
						}

						Vector3? desired = null;
						if (corrections != null && corrections.TryGetValue(nailObject, out var c)) {
							nailObject.position = c.position;
							nailObject.rotation = c.rotation;
							desired = c.desiredLossyScale;
						}
						nailObject.SetParent(target, true);
						if (desired.HasValue) EnforceLossyScale(nailObject, desired.Value);
					}

					foreach (Transform? nailObject in rightFootNailObjects) {
						index++;
						if (nailObject == null) continue;
						Transform? target = targetBoneDictionary[MDNailToolDefines.TARGET_BONE_NAME_LIST[index]];
						if (target == null) {
							ToolConsole.Error("NailSetup", $"Not found target bone : {MDNailToolDefines.TARGET_BONE_NAME_LIST[index]}");
							continue;
						}

						Vector3? desired = null;
						if (corrections != null && corrections.TryGetValue(nailObject, out var c)) {
							nailObject.position = c.position;
							nailObject.rotation = c.rotation;
							desired = c.desiredLossyScale;
						}
						nailObject.SetParent(target, true);
						if (desired.HasValue) EnforceLossyScale(nailObject, desired.Value);
					}
				} else {
					foreach (Transform? nailObject in leftFootNailObjects) {
						if (nailObject == null) continue;
						Object.DestroyImmediate(nailObject.gameObject);
					}

					foreach (Transform? nailObject in rightFootNailObjects) {
						if (nailObject == null) continue;
						Object.DestroyImmediate(nailObject.gameObject);
					}
				}

				Object.DestroyImmediate(nailPrefabObject);
		}
	}
}

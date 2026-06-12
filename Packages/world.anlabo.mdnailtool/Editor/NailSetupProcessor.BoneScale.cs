using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

#nullable enable

namespace world.anlabo.mdnailtool.Editor {
	public partial class NailSetupProcessor {
		// rootとHumanoidボーンのscaleを退避して1に揃える. 部分実行中に例外が起きても呼び出し側 finally で復元できるよう, 渡された辞書に直接逐次追加する.
		private static void SaveAndNeutralizeBoneScales(VRCAvatarDescriptor avatar, Dictionary<Transform, Vector3> saved)
		{
			if (avatar == null) return;

			if (avatar.transform.localScale != Vector3.one)
			{
				saved[avatar.transform] = avatar.transform.localScale;
				avatar.transform.localScale = Vector3.one;
			}

			Animator anim = avatar.GetComponent<Animator>();
			if (anim == null || anim.avatar == null || !anim.avatar.isHuman) return;

			foreach (HumanBodyBones hbb in System.Enum.GetValues(typeof(HumanBodyBones)))
			{
				if (hbb == HumanBodyBones.LastBone) continue;
				Transform? t = anim.GetBoneTransform(hbb);
				if (t == null) continue;
				if (t.localScale != Vector3.one)
				{
					saved[t] = t.localScale;
					t.localScale = Vector3.one;
				}
			}
		}

		// scale復元
		private static void RestoreBoneScales(Dictionary<Transform, Vector3> saved)
		{
			if (saved == null) return;
			foreach (var kv in saved)
			{
				if (kv.Key != null) kv.Key.localScale = kv.Value;
			}
		}

		// 表示範囲を広めに固定。カリング対策
		private static void ApplyNailBoundsGuard(Transform?[] nailObjects) {
			var guardBounds = new Bounds(Vector3.zero, Vector3.one);
			foreach (Transform? nailObject in nailObjects) {
				if (nailObject == null) continue;
				SkinnedMeshRenderer? smr = nailObject.GetComponent<SkinnedMeshRenderer>();
				if (smr == null) continue;
				smr.localBounds = guardBounds;
			}
		}
	}
}

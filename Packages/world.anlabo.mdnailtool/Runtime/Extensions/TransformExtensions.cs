#nullable enable

using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace world.anlabo.mdnailtool.Runtime.Extensions {
	public static class TransformExtensions {
		public static Transform? FindRecursive(this Transform transform, string name) {

			Transform? ret = transform.Find(name);
			if (ret != null) return ret;
			
			foreach (Transform child in transform) {
				ret = child.FindRecursive(name);
				if (ret != null) return ret;
			}

			return null;
		}

		public static IEnumerable<Transform?> FindRecursiveWithRegex(this Transform transform, string pattern) {
			Regex regex = new(pattern);
			return transform.FindRecursiveWithRegex(regex);
		}

		private static IEnumerable<Transform?> FindRecursiveWithRegex(this Transform transform, Regex regex) {
			Queue<Transform?> queue = new();
			queue.Enqueue(transform);

			while (queue.Count != 0) {
				Transform? current = queue.Dequeue();
				if (current == null) continue;
				foreach (Transform? child in current) {
					if (child == null) continue;
					if (regex.Match(child.name).Success) {
						yield return child;
					}
					if (child == null) continue;
					queue.Enqueue(child);
				}
			}
		}
	}
}
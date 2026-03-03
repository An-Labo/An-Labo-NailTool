using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity
{
	/// <summary>
	/// 追加オブジェクト・追加マテリアルの名前→GUID(複数)レジストリ。
	/// additionalAssets.json から読み込む。
	/// 1つの名前に複数GUIDをセット登録できる。
	/// </summary>
	[JsonObject]
	public class AdditionalAssetRegistry
	{
		[JsonProperty("objects")]
		[JsonConverter(typeof(AdditionalObjectDictionaryConverter))]
		public Dictionary<string, AdditionalObjectEntry>? Objects { get; set; }

		[JsonProperty("materials")]
		public Dictionary<string, List<string>>? Materials { get; set; }

		/// <summary>
		/// 名前またはGUIDを受け取り、オブジェクト用GUIDリストに解決する（指を指定しない旧互換版）。
		/// </summary>
		public IReadOnlyList<string> ResolveObjectGuids(string nameOrGuid)
		{
			if (Objects != null && Objects.TryGetValue(nameOrGuid, out AdditionalObjectEntry? entry))
			{
				if (entry.Guids != null) return entry.Guids;
				if (entry.Fingers != null && entry.Fingers.TryGetValue("default", out List<string>? dg)) return dg;
				return Array.Empty<string>();
			}
			return new[] { nameOrGuid };
		}

		/// <summary>
		/// 指インデックスを指定してGUIDリストを解決する。
		/// 指別データがあればそちらを優先し、なければデフォルトにフォールバック。
		/// </summary>
		public IReadOnlyList<string> ResolveObjectGuids(string nameOrGuid, int fingerIndex)
		{
			if (Objects != null && Objects.TryGetValue(nameOrGuid, out AdditionalObjectEntry? entry))
				return entry.ResolveGuidsForFinger(fingerIndex);
			return new[] { nameOrGuid };
		}

		/// <summary>
		/// 指定指インデックスで使用可能なオブジェクト名のリストを返す。
		/// </summary>
		public IReadOnlyList<string> GetAllowedObjectNames(int fingerIndex)
		{
			if (Objects == null) return Array.Empty<string>();
			var result = new List<string>();
			foreach (var kv in Objects)
			{
				if (kv.Value.IsAllowedForFinger(fingerIndex))
					result.Add(kv.Key);
			}
			return result;
		}

		/// <summary>
		/// 名前またはGUIDを受け取り、マテリアル用GUIDリストに解決する。
		/// </summary>
		public IReadOnlyList<string> ResolveMaterialGuids(string nameOrGuid)
		{
			if (Materials != null && Materials.TryGetValue(nameOrGuid, out List<string>? guids))
				return guids;
			return new[] { nameOrGuid };
		}

		/// <summary>
		/// GUIDまたは名前のリストから、対応するレジストリ名のセットを返す。
		/// 値がレジストリ名ならそのまま、GUIDなら逆引きして名前を返す。
		/// </summary>
		public HashSet<string> FindMaterialNames(IEnumerable<string> guidOrNames)
		{
			var result = new HashSet<string>();
			foreach (string v in guidOrNames)
			{
				if (Materials != null && Materials.ContainsKey(v))
				{
					result.Add(v);
					continue;
				}
				string? name = ReverseLookupMaterial(v);
				if (name != null) result.Add(name);
			}
			return result;
		}

		/// <summary>
		/// GUIDまたは名前のリストから、対応するレジストリ名のセットを返す（オブジェクト用）。
		/// </summary>
		public HashSet<string> FindObjectNames(IEnumerable<string> guidOrNames)
		{
			var result = new HashSet<string>();
			foreach (string v in guidOrNames)
			{
				if (Objects != null && Objects.ContainsKey(v))
				{
					result.Add(v);
					continue;
				}
				string? name = ReverseLookupObject(v);
				if (name != null) result.Add(name);
			}
			return result;
		}

		private string? ReverseLookupMaterial(string guid)
		{
			if (Materials == null) return null;
			foreach (var kv in Materials)
				if (kv.Value.Contains(guid)) return kv.Key;
			return null;
		}

		private string? ReverseLookupObject(string guid)
		{
			if (Objects == null) return null;
			foreach (var kv in Objects)
			{
				AdditionalObjectEntry entry = kv.Value;
				if (entry.Guids != null && entry.Guids.Contains(guid)) return kv.Key;
				if (entry.Fingers != null)
				{
					foreach (List<string> fg in entry.Fingers.Values)
						if (fg.Contains(guid)) return kv.Key;
				}
			}
			return null;
		}
	}
}

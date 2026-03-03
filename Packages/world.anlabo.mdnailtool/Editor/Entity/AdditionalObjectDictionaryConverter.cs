using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable

namespace world.anlabo.mdnailtool.Editor.Entity
{
	/// <summary>
	/// AdditionalAssetRegistry の "objects" フィールドをデシリアライズする。
	/// 旧形式（配列）と新形式（オブジェクト）の両方に対応する。
	///
	/// 旧形式: "name": ["guid1", "guid2"]
	/// 新形式: "name": { "allowedFingers": [...], "fingers": {...}, "guids": [...] }
	/// </summary>
	public class AdditionalObjectDictionaryConverter
		: JsonConverter<Dictionary<string, AdditionalObjectEntry>>
	{
		public override Dictionary<string, AdditionalObjectEntry>? ReadJson(
			JsonReader reader, Type objectType,
			Dictionary<string, AdditionalObjectEntry>? existingValue,
			bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;

			var result = new Dictionary<string, AdditionalObjectEntry>();
			JObject obj = JObject.Load(reader);

			foreach (JProperty property in obj.Properties())
			{
				string name = property.Name;
				JToken value = property.Value;

				if (value.Type == JTokenType.Array)
				{
					// 旧形式: "name": ["guid1", "guid2"]
					List<string> guids = value.ToObject<List<string>>(serializer)
					                     ?? new List<string>();
					result[name] = AdditionalObjectEntry.FromLegacyGuids(guids);
				}
				else if (value.Type == JTokenType.Object)
				{
					// 新形式: "name": { "allowedFingers": [...], "fingers": {...} }
					AdditionalObjectEntry entry = value.ToObject<AdditionalObjectEntry>(serializer)
					                              ?? new AdditionalObjectEntry();
					result[name] = entry;
				}
			}

			return result;
		}

		public override void WriteJson(JsonWriter writer,
			Dictionary<string, AdditionalObjectEntry>? value,
			JsonSerializer serializer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			writer.WriteStartObject();
			foreach (var kv in value)
			{
				writer.WritePropertyName(kv.Key);
				AdditionalObjectEntry entry = kv.Value;

				// 旧形式互換: AllowedFingers も Fingers も無い場合は配列で出力
				if (entry.AllowedFingers == null && entry.Fingers == null && entry.Guids != null)
				{
					serializer.Serialize(writer, entry.Guids);
				}
				else
				{
					serializer.Serialize(writer, entry);
				}
			}
			writer.WriteEndObject();
		}
	}
}

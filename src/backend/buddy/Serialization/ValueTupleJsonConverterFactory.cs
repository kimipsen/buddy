using System.Text.Json;
using System.Text.Json.Serialization;

namespace buddy.Serialization;

/// <summary>
/// Serializes a 2-element ValueTuple (e.g. the (DateOnly Date, MealSlot Slot) dictionary keys used
/// by <c>MealPlan.Assignments</c> and <c>MealplanAiSession.Draft</c>) to/from JSON. Plain
/// System.Text.Json has no built-in converter for ValueTuple and throws
/// <see cref="NotSupportedException"/> the moment one is serialized -- most visibly as a
/// Dictionary key, where STJ additionally requires the key's converter to implement
/// <see cref="JsonConverter{T}.ReadAsPropertyName"/>/<see cref="JsonConverter{T}.WriteAsPropertyName"/>
/// rather than the ordinary Read/Write pair, since JSON object keys are always strings. Mirrors
/// <see cref="StronglyTypedIdJsonConverterFactory"/>'s shape for the same reason.
/// </summary>
public sealed class ValueTupleJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(ValueTuple<,>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(Converter<,>).MakeGenericType(typeToConvert.GetGenericArguments());
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class Converter<T1, T2> : JsonConverter<(T1, T2)>
    {
        // Ordinary value position: JSON array form, e.g. [ "2024-01-01", "Breakfast" ].
        public override (T1, T2) Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Expected the start of a 2-element JSON array for {typeToConvert}.");
            }

            reader.Read();
            var item1 = JsonSerializer.Deserialize<T1>(ref reader, options)!;

            reader.Read();
            var item2 = JsonSerializer.Deserialize<T2>(ref reader, options)!;

            reader.Read();
            if (reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException($"Expected the end of a 2-element JSON array for {typeToConvert}.");
            }

            return (item1, item2);
        }

        public override void Write(Utf8JsonWriter writer, (T1, T2) value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            JsonSerializer.Serialize(writer, value.Item1, options);
            JsonSerializer.Serialize(writer, value.Item2, options);
            writer.WriteEndArray();
        }

        // Needed to serialize a Dictionary<(T1, T2), ...> (e.g. MealPlan.Assignments:
        // ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealPlanAssignment>) -- JSON object
        // keys are always strings, so the tuple is encoded as "<item1>|<item2>", each item run
        // through its own converter (e.g. DateOnly's "yyyy-MM-dd", MealSlot's enum-as-string) and
        // unquoted, rather than the array form Read/Write use for an ordinary value. Every item
        // type this converter is actually applied to in this codebase (DateOnly, MealSlot) has a
        // string JSON representation containing no '|', so a plain Split is unambiguous.
        public override (T1, T2) ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var propertyName = reader.GetString()!;
            var parts = propertyName.Split('|', 2);

            if (parts.Length != 2)
            {
                throw new JsonException($"Expected a '|'-separated 2-element property name for {typeToConvert}, got \"{propertyName}\".");
            }

            var item1 = JsonSerializer.Deserialize<T1>(JsonSerializer.Serialize(parts[0]), options)!;
            var item2 = JsonSerializer.Deserialize<T2>(JsonSerializer.Serialize(parts[1]), options)!;

            return (item1, item2);
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, (T1, T2) value, JsonSerializerOptions options)
        {
            var item1 = Unquote(JsonSerializer.Serialize(value.Item1, options));
            var item2 = Unquote(JsonSerializer.Serialize(value.Item2, options));

            writer.WritePropertyName($"{item1}|{item2}");
        }

        private static string Unquote(string json) =>
            json.Length >= 2 && json[0] == '"' ? JsonSerializer.Deserialize<string>(json)! : json;
    }
}

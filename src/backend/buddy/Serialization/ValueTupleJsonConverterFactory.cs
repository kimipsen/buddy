using System.Text.Json;
using System.Text.Json.Serialization;

namespace buddy.Serialization;

/// <summary>
/// Serializes a <c>ValueTuple</c> (e.g. <c>(DateOnly Date, MealSlot Slot)</c>, used throughout
/// this codebase as a composite dictionary key or hash-set element) as a JSON array, and
/// reconstructs it on read. Without this, <c>System.Text.Json</c> either throws
/// <see cref="NotSupportedException"/> (as a dictionary key) or silently serializes the tuple as
/// an empty object (as a plain value/set element, since <c>ItemN</c> are public fields, not
/// properties) -- the latter is a real data-loss trap, not just a missing feature.
/// </summary>
public sealed class ValueTupleJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType
        && typeToConvert.Namespace == "System"
        && typeToConvert.Name.StartsWith("ValueTuple`", StringComparison.Ordinal);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var elementTypes = typeToConvert.GetGenericArguments();
        var converterType = elementTypes.Length switch
        {
            2 => typeof(Converter2<,>).MakeGenericType(elementTypes),
            3 => typeof(Converter3<,,>).MakeGenericType(elementTypes),
            _ => throw new NotSupportedException(
                $"No tuple JSON converter registered for arity {elementTypes.Length} ({typeToConvert}). Add one if a new tuple shape shows up.")
        };

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class Converter2<T1, T2> : JsonConverter<(T1, T2)>
    {
        public override (T1, T2) Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            reader.Read();
            var item1 = JsonSerializer.Deserialize<T1>(ref reader, options);
            reader.Read();
            var item2 = JsonSerializer.Deserialize<T2>(ref reader, options);
            reader.Read();
            return (item1!, item2!);
        }

        public override void Write(Utf8JsonWriter writer, (T1, T2) value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            JsonSerializer.Serialize(writer, value.Item1, options);
            JsonSerializer.Serialize(writer, value.Item2, options);
            writer.WriteEndArray();
        }

        // Same reasoning as StronglyTypedIdJsonConverterFactory.WriteAsPropertyName/ReadAsPropertyName:
        // encode the whole tuple through this converter's own array form and use that as the raw
        // property-name text, rather than hand-rolling a delimiter scheme.
        public override (T1, T2) ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            JsonSerializer.Deserialize<(T1, T2)>(reader.GetString()!, options);

        public override void WriteAsPropertyName(Utf8JsonWriter writer, (T1, T2) value, JsonSerializerOptions options) =>
            writer.WritePropertyName(JsonSerializer.Serialize(value, options));
    }

    private sealed class Converter3<T1, T2, T3> : JsonConverter<(T1, T2, T3)>
    {
        public override (T1, T2, T3) Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            reader.Read();
            var item1 = JsonSerializer.Deserialize<T1>(ref reader, options);
            reader.Read();
            var item2 = JsonSerializer.Deserialize<T2>(ref reader, options);
            reader.Read();
            var item3 = JsonSerializer.Deserialize<T3>(ref reader, options);
            reader.Read();
            return (item1!, item2!, item3!);
        }

        public override void Write(Utf8JsonWriter writer, (T1, T2, T3) value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            JsonSerializer.Serialize(writer, value.Item1, options);
            JsonSerializer.Serialize(writer, value.Item2, options);
            JsonSerializer.Serialize(writer, value.Item3, options);
            writer.WriteEndArray();
        }

        public override (T1, T2, T3) ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            JsonSerializer.Deserialize<(T1, T2, T3)>(reader.GetString()!, options);

        public override void WriteAsPropertyName(Utf8JsonWriter writer, (T1, T2, T3) value, JsonSerializerOptions options) =>
            writer.WritePropertyName(JsonSerializer.Serialize(value, options));
    }
}

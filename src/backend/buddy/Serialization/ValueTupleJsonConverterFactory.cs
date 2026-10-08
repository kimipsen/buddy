using System.Runtime.CompilerServices;
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
    // ValueTuple`8 nests items 8+ in a TRest tuple; nothing here uses one, so it isn't handled.
    private const int MaxArity = 7;

    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType
        && typeToConvert.Namespace == "System"
        && typeToConvert.Name.StartsWith("ValueTuple`", StringComparison.Ordinal);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert.GetGenericArguments().Length > MaxArity)
        {
            throw new NotSupportedException(
                $"Tuples with more than {MaxArity} items aren't supported ({typeToConvert}).");
        }

        return (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class Converter<TTuple> : JsonConverter<TTuple>
        where TTuple : struct, ITuple
    {
        private static readonly Type[] ItemTypes = typeof(TTuple).GetGenericArguments();

        public override TTuple Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var items = new object?[ItemTypes.Length];
            for (var i = 0; i < items.Length; i++)
            {
                reader.Read();
                items[i] = JsonSerializer.Deserialize(ref reader, ItemTypes[i], options);
            }

            reader.Read();
            return (TTuple)Activator.CreateInstance(typeof(TTuple), items)!;
        }

        public override void Write(Utf8JsonWriter writer, TTuple value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            for (var i = 0; i < ItemTypes.Length; i++)
            {
                JsonSerializer.Serialize(writer, value[i], ItemTypes[i], options);
            }

            writer.WriteEndArray();
        }

        // Same reasoning as StronglyTypedIdJsonConverterFactory.WriteAsPropertyName/ReadAsPropertyName:
        // encode the whole tuple through this converter's own array form and use that as the raw
        // property-name text, rather than hand-rolling a delimiter scheme.
        public override TTuple ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            JsonSerializer.Deserialize<TTuple>(reader.GetString()!, options);

        public override void WriteAsPropertyName(Utf8JsonWriter writer, TTuple value, JsonSerializerOptions options) =>
            writer.WritePropertyName(JsonSerializer.Serialize(value, options));
    }
}

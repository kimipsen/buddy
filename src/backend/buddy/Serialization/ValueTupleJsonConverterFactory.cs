using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace buddy.Serialization;

/// <summary>
/// Adds System.Text.Json support for <c>ValueTuple</c>-shaped values (including C#'s named-tuple
/// sugar, e.g. <c>(DateOnly Date, PickupSlot Slot)</c>, which compiles to the same
/// <see cref="ValueTuple{T1, T2}"/> family). Plain System.Text.Json throws
/// <see cref="NotSupportedException"/> for these -- they have no parameterless constructor and
/// their "ItemN" fields aren't picked up as serializable members. Writes/reads a tuple positionally
/// as a JSON array, and -- mirroring <see cref="StronglyTypedIdJsonConverterFactory"/> -- also
/// supports using a tuple as a dictionary key (e.g. <c>ImmutableDictionary&lt;(DateOnly, PickupSlot),
/// PickupAssignment&gt;</c>) by encoding that same array as the JSON property-name string.
/// </summary>
public sealed class ValueTupleJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsValueType
        && typeToConvert.IsGenericType
        && typeToConvert.FullName is { } name
        && name.StartsWith("System.ValueTuple`", StringComparison.Ordinal);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(Converter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class Converter<TTuple> : JsonConverter<TTuple>
        where TTuple : struct
    {
        // ValueTuple's element fields are always literally named Item1, Item2, ... regardless of
        // the C# tuple-element names (Date, Slot, ...) used at the call site -- those names are
        // compiler metadata, not real field names -- so ordering by name gives positional order.
        private static readonly FieldInfo[] Fields =
        [
            .. typeof(TTuple)
                .GetFields()
                .Where(f => f.Name.StartsWith("Item", StringComparison.Ordinal))
                .OrderBy(f => f.Name, StringComparer.Ordinal)
        ];

        public override TTuple Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Expected a JSON array to deserialize {typeToConvert}.");
            }

            object boxed = default(TTuple);
            var index = 0;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (index >= Fields.Length)
                {
                    throw new JsonException($"Too many elements in the JSON array for {typeToConvert}.");
                }

                var field = Fields[index++];
                var value = JsonSerializer.Deserialize(ref reader, field.FieldType, options);
                field.SetValue(boxed, value);
            }

            return (TTuple)boxed;
        }

        public override void Write(Utf8JsonWriter writer, TTuple value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            foreach (var field in Fields)
            {
                JsonSerializer.Serialize(writer, field.GetValue(value), field.FieldType, options);
            }

            writer.WriteEndArray();
        }

        public override TTuple ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var propertyName = reader.GetString()!;
            var bytes = Encoding.UTF8.GetBytes(propertyName);
            var elementReader = new Utf8JsonReader(bytes);
            elementReader.Read();

            return Read(ref elementReader, typeToConvert, options);
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, TTuple value, JsonSerializerOptions options)
        {
            using var stream = new MemoryStream();

            using (var elementWriter = new Utf8JsonWriter(stream))
            {
                Write(elementWriter, value, options);
            }

            writer.WritePropertyName(Encoding.UTF8.GetString(stream.ToArray()));
        }
    }
}

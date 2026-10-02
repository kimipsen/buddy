using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace buddy.Serialization;

// HTTP request/response DTO hierarchies shaped as an object with a numeric "kind" plus that case's
// own fields -- e.g. PickupAssigneeDto { "kind": 0, "guardianId": "..." }. Hand-written rather than
// [JsonPolymorphic]: with an abstract base, a body missing "kind" makes System.Text.Json throw
// NotSupportedException, which minimal APIs don't treat as a binding failure (a 500, not a 400).
// Here a missing, non-numeric or unknown kind is a JsonException, so it becomes a 400
// validation_error. "kind" may appear anywhere in the object; on a duplicate the last one wins, as
// in the rest of System.Text.Json's object binding. The case's own fields still bind through the
// configured options (naming policy, RespectRequiredConstructorParameters).
public abstract class KindDiscriminatedJsonConverter<TBase> : JsonConverter<TBase>
    where TBase : class
{
    private const string KindProperty = "kind";

    // Each case's kind ordinal and concrete type.
    protected abstract IReadOnlyDictionary<int, Type> Cases { get; }

    public override TBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"A {typeof(TBase).Name} must be a JSON object.");
        }

        var kind = root.EnumerateObject()
            .Where(property => string.Equals(property.Name, KindProperty, StringComparison.OrdinalIgnoreCase))
            .Select(property => property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var value) ? (int?)value : null)
            .LastOrDefault()
            ?? throw new JsonException($"A {typeof(TBase).Name} requires a numeric kind.");

        if (!Cases.TryGetValue(kind, out var caseType))
        {
            throw new JsonException($"Unknown {typeof(TBase).Name} kind: {kind}.");
        }

        // The case binds from the object without "kind", so a case type may opt into
        // [JsonUnmappedMemberHandling(Disallow)] to reject fields that belong to another case. A
        // failure is rethrown without its element-relative path, so the outer serializer stamps
        // the real one (e.g. "$.assignee") and RequestBindingFailureMiddleware reports e.g.
        // "assignee.guardianId".
        var fields = new JsonObject();

        foreach (var property in root.EnumerateObject().Where(property => !string.Equals(property.Name, KindProperty, StringComparison.OrdinalIgnoreCase)))
        {
            fields[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        }

        try
        {
            return (TBase?)fields.Deserialize(caseType, options)
                ?? throw new JsonException($"A {caseType.Name} was null.");
        }
        catch (JsonException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
    {
        var kind = Cases.Where(pair => pair.Value == value.GetType()).Select(pair => (int?)pair.Key).SingleOrDefault()
            ?? throw new UnreachableException($"Unmapped {typeof(TBase).Name} case: {value.GetType().Name}.");

        writer.WriteStartObject();
        writer.WriteNumber(options.PropertyNamingPolicy?.ConvertName(KindProperty) ?? KindProperty, kind);

        // The concrete case type has no converter of its own (the attribute sits on the base), so
        // this serializes its fields normally.
        foreach (var property in JsonSerializer.SerializeToElement(value, value.GetType(), options).EnumerateObject())
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}

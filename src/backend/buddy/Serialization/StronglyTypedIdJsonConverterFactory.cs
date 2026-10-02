using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace buddy.Serialization;

/// <summary>
/// Serializes single-value wrapper types (e.g. <c>record UserId(Guid Value)</c>) as their
/// underlying value instead of as a JSON object, and reconstructs the wrapper on read.
/// Matches any type whose sole public constructor takes one parameter named "Value" backed
/// by a same-typed "Value" property, so it covers every strongly-typed id without a
/// per-type converter.
/// </summary>
public sealed class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => GetValueProperty(typeToConvert) is not null;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = GetValueProperty(typeToConvert)!.PropertyType;
        var converterType = typeof(Converter<,>).MakeGenericType(typeToConvert, valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private static PropertyInfo? GetValueProperty(Type type)
    {
        // Every genuine strongly-typed id in this codebase (UserId, CalendarId, ...) is a
        // top-level type. A nested type with the same single-"Value"-parameter shape is a
        // different pattern entirely -- a union case wrapping one id, e.g.
        // PrintTemplateOwner.User(UserId Value) -- and must NOT be swallowed by this factory: doing so
        // strips the case's own JSON shape, which then breaks the union's type-classifier
        // deserialization (System.Text.Json's JsonUnionConverter needs the case's normal object
        // shape to tell cases apart). Excluding DeclaringType != null keeps this factory scoped to
        // actual id wrappers only.
        if (type.DeclaringType is not null)
        {
            return null;
        }

        if (type.GetConstructors() is not [var ctor] || ctor.GetParameters() is not [{ Name: "Value" } parameter])
        {
            return null;
        }

        var property = type.GetProperty("Value");
        return property is not null && property.PropertyType == parameter.ParameterType ? property : null;
    }

    private sealed class Converter<TId, TValue> : JsonConverter<TId>
    {
        private static readonly ConstructorInfo Constructor = typeof(TId).GetConstructor([typeof(TValue)])!;
        private static readonly PropertyInfo ValueProperty = typeof(TId).GetProperty("Value")!;

        public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
            return (TId)Constructor.Invoke([value]);
        }

        public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, ValueProperty.GetValue(value), options);

        // Needed to serialize a Dictionary<TId, ...> (e.g. Group.Members: ImmutableDictionary<UserId,
        // GroupRole>) -- JSON object keys are always strings, so this re-encodes the raw property-name
        // text as the JSON string token TValue's own converter expects, rather than trying to run
        // TValue's normal (non-property-name) Read/Write, which assumes a full JSON value, not a bare
        // string. Every wrapper Value type in this codebase is Guid, which is exactly that shape.
        public override TId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var propertyName = reader.GetString()!;
            var value = JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(propertyName), options);
            return (TId)Constructor.Invoke([value]);
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, TId value, JsonSerializerOptions options)
        {
            var json = JsonSerializer.Serialize(ValueProperty.GetValue(value), options);
            var name = json.Length >= 2 && json[0] == '"' ? JsonSerializer.Deserialize<string>(json)! : json;
            writer.WritePropertyName(name);
        }
    }
}

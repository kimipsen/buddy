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
    }
}

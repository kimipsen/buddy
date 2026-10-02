using System.Text.Json;
using System.Text.Json.Serialization;

namespace buddy.Features.WorkLocations;

// Marten's (de)serialization of WorkDayOverride in WorkLocationOverridden/-Cleared and the
// WorkLocationSchedule snapshot, with an explicit Kind discriminator: {"Kind":"DayOff"} or
// {"Kind":"AtLocation","LocationId":guid}. LocationId goes through the configured options (the
// stores' StronglyTypedIdJsonConverterFactory).
public sealed class WorkDayOverrideJsonConverter : JsonConverter<WorkDayOverride>
{
    public override WorkDayOverride Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = Required(root, "Kind").GetString();

        return kind switch
        {
            nameof(WorkDayOverride.AtLocation) => new WorkDayOverride.AtLocation(
                Required(root, "LocationId").Deserialize<WorkLocationId>(options)
                    ?? throw new JsonException("WorkDayOverride LocationId is null.")),
            nameof(WorkDayOverride.DayOff) => new WorkDayOverride.DayOff(),
            _ => throw new JsonException($"Unknown WorkDayOverride Kind discriminator: '{kind}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, WorkDayOverride value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case WorkDayOverride.AtLocation atLocation:
                writer.WriteString("Kind", nameof(WorkDayOverride.AtLocation));
                writer.WritePropertyName("LocationId");
                JsonSerializer.Serialize(writer, atLocation.LocationId, options);
                break;

            case WorkDayOverride.DayOff:
                writer.WriteString("Kind", nameof(WorkDayOverride.DayOff));
                break;

            default:
                throw new JsonException("Cannot serialize an uninitialized WorkDayOverride.");
        }

        writer.WriteEndObject();
    }

    private static JsonElement Required(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value
            : throw new JsonException($"WorkDayOverride is missing its '{name}' property.");
}

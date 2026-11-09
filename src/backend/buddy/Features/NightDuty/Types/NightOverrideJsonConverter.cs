using System.Text.Json;
using System.Text.Json.Serialization;

namespace buddy.Features.NightDuty;

// Marten's (de)serialization of NightOverride in NightOverridden/-Cleared and the NightSchedule
// snapshot, with an explicit Kind discriminator: {"Kind":"Cleared"} or
// {"Kind":"Planned","Entry":{...}}. Entry goes through the configured options, so its
// NightResponsible uses NightResponsibleJsonConverter. Same shape as WorkDayOverrideJsonConverter.
public sealed class NightOverrideJsonConverter : JsonConverter<NightOverride>
{
    public override NightOverride Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = Required(root, "Kind").GetString();

        return kind switch
        {
            nameof(NightOverride.Planned) => new NightOverride.Planned(
                Required(root, "Entry").Deserialize<NightEntry>(options)
                    ?? throw new JsonException("NightOverride Entry is null.")),
            nameof(NightOverride.Cleared) => new NightOverride.Cleared(),
            _ => throw new JsonException($"Unknown NightOverride Kind discriminator: '{kind}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, NightOverride value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case NightOverride.Planned planned:
                writer.WriteString("Kind", nameof(NightOverride.Planned));
                writer.WritePropertyName("Entry");
                JsonSerializer.Serialize(writer, planned.Entry, options);
                break;

            case NightOverride.Cleared:
                writer.WriteString("Kind", nameof(NightOverride.Cleared));
                break;

            default:
                throw new JsonException("Cannot serialize an uninitialized NightOverride.");
        }

        writer.WriteEndObject();
    }

    private static JsonElement Required(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value
            : throw new JsonException($"NightOverride is missing its '{name}' property.");
}

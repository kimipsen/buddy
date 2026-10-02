using System.Text.Json;
using System.Text.Json.Serialization;

namespace buddy.Features.Calendars;

// Marten's (de)serialization of CompletionTarget -- in TaskCompletionChanged, StarAwarded/StarRevoked
// and the CalendarItem/ChildProgress snapshots -- with an explicit Kind discriminator (the union's
// own converter can only classify cases by JSON shape, and WholeTask has none).
public sealed class CompletionTargetJsonConverter : JsonConverter<CompletionTarget>
{
    public override CompletionTarget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = root.TryGetProperty("Kind", out var value) ? value.GetString() : throw new JsonException("CompletionTarget is missing 'Kind'.");

        return kind switch
        {
            nameof(CompletionTarget.WholeTask) => new CompletionTarget.WholeTask(),
            nameof(CompletionTarget.Subtask) => new CompletionTarget.Subtask(
                root.TryGetProperty("SubtaskId", out var subtaskId)
                    ? subtaskId.GetGuid()
                    : throw new JsonException("CompletionTarget.Subtask is missing 'SubtaskId'.")),
            _ => throw new JsonException($"Unknown CompletionTarget Kind discriminator: '{kind}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, CompletionTarget value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case CompletionTarget.WholeTask:
                writer.WriteString("Kind", nameof(CompletionTarget.WholeTask));
                break;

            case CompletionTarget.Subtask subtask:
                writer.WriteString("Kind", nameof(CompletionTarget.Subtask));
                writer.WriteString("SubtaskId", subtask.SubtaskId);
                break;

            // A default(CompletionTarget) holds no case: refuse it rather than persist an unreadable object.
            default:
                throw new JsonException("Cannot serialize a CompletionTarget that holds no case.");
        }

        writer.WriteEndObject();
    }
}

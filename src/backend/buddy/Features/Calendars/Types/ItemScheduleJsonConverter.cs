using System.Text.Json;
using System.Text.Json.Serialization;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

// Marten's (de)serialization of ItemSchedule inside the CalendarItem snapshot, with an explicit Kind
// discriminator (System.Text.Json's union converter classifies cases by JSON shape only). The
// nested Period/DueDate go through the configured options. A task's TaskSource is encoded by the
// presence of TaskTemplateId: absent means Freeform.
public sealed class ItemScheduleJsonConverter : JsonConverter<ItemSchedule>
{
    public override ItemSchedule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = Required(root, "Kind").GetString();

        return kind switch
        {
            nameof(ItemSchedule.Event) => new ItemSchedule.Event(Deserialize<Period>(root, "Period", options)),
            nameof(ItemSchedule.Task) => new ItemSchedule.Task(
                Deserialize<DueDate>(root, "DueDate", options),
                root.TryGetProperty("AssignedTo", out var assignedTo) ? new UserId(assignedTo.GetGuid()) : null,
                root.TryGetProperty("TaskTemplateId", out var templateId)
                    ? new TaskSource.FromTemplate(templateId.GetGuid())
                    : new TaskSource.Freeform()),
            _ => throw new JsonException($"Unknown ItemSchedule Kind discriminator: '{kind}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, ItemSchedule value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case ItemSchedule.Event @event:
                writer.WriteString("Kind", nameof(ItemSchedule.Event));
                writer.WritePropertyName("Period");
                JsonSerializer.Serialize(writer, @event.Period, options);
                break;

            case ItemSchedule.Task task:
                writer.WriteString("Kind", nameof(ItemSchedule.Task));
                writer.WritePropertyName("DueDate");
                JsonSerializer.Serialize(writer, task.DueDate, options);

                if (task.AssignedTo is { } assignedTo)
                {
                    writer.WriteString("AssignedTo", assignedTo.Value);
                }

                switch (task.Source)
                {
                    case TaskSource.FromTemplate fromTemplate:
                        writer.WriteString("TaskTemplateId", fromTemplate.TaskTemplateId);
                        break;
                    case TaskSource.Freeform:
                        break;
                    default:
                        throw new JsonException("Cannot serialize a TaskSource that holds no case.");
                }

                break;

            // A default(ItemSchedule) holds no case: refuse it rather than persist an unreadable object.
            default:
                throw new JsonException("Cannot serialize an ItemSchedule that holds no case.");
        }

        writer.WriteEndObject();
    }

    private static JsonElement Required(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value : throw new JsonException($"ItemSchedule is missing '{name}'.");

    private static T Deserialize<T>(JsonElement root, string name, JsonSerializerOptions options) =>
        Required(root, name).Deserialize<T>(options) ?? throw new JsonException($"ItemSchedule '{name}' is null.");
}

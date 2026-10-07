using System.Text.Json;
using System.Text.Json.Serialization;

namespace buddy.Features.Calendars;

// Marten's (de)serialization of Recurrence in the calendar item events and snapshot, with explicit
// Kind discriminators: {"Kind":"OneOff"} or {"Kind":"Repeating","Frequency":"Weekly",
// "IntervalCount":2,"End":{"Kind":"Never"}}, where End may instead be {"Kind":"On","Until":date}.
// Frequency goes through the configured options (the stores' JsonStringEnumConverter). Weekdays is
// written as day names ("Weekdays":["Monday","Friday"]) only when it isn't None, and a missing
// property reads as None, so events written before the weekday filter keep their exact shape.
public sealed class RecurrenceJsonConverter : JsonConverter<Recurrence>
{
    public override Recurrence Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var kind = Required(root, "Kind").GetString();

        return kind switch
        {
            nameof(Recurrence.OneOff) => new Recurrence.OneOff(),
            nameof(Recurrence.Repeating) => new Recurrence.Repeating(
                Required(root, "Frequency").Deserialize<RecurrenceFrequency>(options),
                Required(root, "IntervalCount").GetInt32(),
                ReadEnd(Required(root, "End"), options),
                ReadWeekdays(root)),
            _ => throw new JsonException($"Unknown Recurrence Kind discriminator: '{kind}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, Recurrence value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case Recurrence.OneOff:
                writer.WriteString("Kind", nameof(Recurrence.OneOff));
                break;

            case Recurrence.Repeating repeating:
                writer.WriteString("Kind", nameof(Recurrence.Repeating));
                writer.WritePropertyName("Frequency");
                JsonSerializer.Serialize(writer, repeating.Frequency, options);
                writer.WriteNumber("IntervalCount", repeating.IntervalCount);
                writer.WritePropertyName("End");
                WriteEnd(writer, repeating.End, options);

                if (repeating.Weekdays != Weekdays.None)
                {
                    WriteWeekdays(writer, repeating.Weekdays);
                }

                break;

            default:
                throw new JsonException("Cannot serialize an uninitialized Recurrence.");
        }

        writer.WriteEndObject();
    }

    private static RecurrenceEnd ReadEnd(JsonElement end, JsonSerializerOptions options)
    {
        var kind = Required(end, "Kind").GetString();

        return kind switch
        {
            nameof(RecurrenceEnd.Never) => new RecurrenceEnd.Never(),
            nameof(RecurrenceEnd.On) => new RecurrenceEnd.On(Required(end, "Until").Deserialize<DateOnly>(options)),
            _ => throw new JsonException($"Unknown RecurrenceEnd Kind discriminator: '{kind}'."),
        };
    }

    private static void WriteEnd(Utf8JsonWriter writer, RecurrenceEnd end, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (end)
        {
            case RecurrenceEnd.Never:
                writer.WriteString("Kind", nameof(RecurrenceEnd.Never));
                break;

            case RecurrenceEnd.On on:
                writer.WriteString("Kind", nameof(RecurrenceEnd.On));
                writer.WritePropertyName("Until");
                JsonSerializer.Serialize(writer, on.Until, options);
                break;

            default:
                throw new JsonException("Cannot serialize an uninitialized RecurrenceEnd.");
        }

        writer.WriteEndObject();
    }

    private static Weekdays ReadWeekdays(JsonElement root)
    {
        if (!root.TryGetProperty("Weekdays", out var days))
        {
            return Weekdays.None;
        }

        var weekdays = Weekdays.None;

        foreach (var day in days.EnumerateArray())
        {
            weekdays |= Enum.Parse<DayOfWeek>(day.GetString() ?? throw new JsonException("Recurrence weekday must be a day name.")).ToWeekday();
        }

        return weekdays;
    }

    private static void WriteWeekdays(Utf8JsonWriter writer, Weekdays weekdays)
    {
        writer.WriteStartArray("Weekdays");

        foreach (var day in weekdays.ToDays())
        {
            writer.WriteStringValue(day.ToString());
        }

        writer.WriteEndArray();
    }

    private static JsonElement Required(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value
            : throw new JsonException($"Recurrence is missing its '{name}' property.");
}

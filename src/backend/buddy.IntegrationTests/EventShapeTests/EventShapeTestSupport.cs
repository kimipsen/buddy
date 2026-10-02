using System.Text.Json;
using System.Text.Json.Serialization;

using buddy.Features.Calendars;
using buddy.Features.Pickups;
using buddy.Features.Users;
using buddy.Features.WorkLocations;
using buddy.Serialization;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

// Once an event has been persisted anywhere real, its JSON shape is a durable contract -- Marten
// replays it from the event stream forever. A refactor that renames a property or changes an
// enum's representation can compile fine and pass every behavioral test while quietly breaking
// replay of existing history. These golden-file comparisons make that kind of change show up as
// a diff in the PR instead of a replay failure in production. See
// docs/backend/analysis/integration-testing-strategy.md.
internal static class EventShapeTestSupport
{
    // Mirrors the System.Text.Json configuration the *Feature.cs files pass to
    // options.UseSystemTextJsonForSerialization(enumStorage: EnumStorage.AsString, ...) for their
    // Marten stores -- enums as their name, strongly-typed ids unwrapped to their raw value, and
    // the union of the extra converters individual stores register: ValueTuples (Medicines,
    // Mealplans, Pickups), PickupAssignee (Pickups), CompletionTarget (Calendars, Progress), Recurrence (Calendars) EmailVerification (Users, snapshot only) and WorkDayOverride (WorkLocations).
    // Each extra converter only handles its own type, so registering all of them here can't change
    // the shape of an event from a store that doesn't register it.
    public static JsonSerializerOptions CreateEventSerializerOptions() => new()
    {
        Converters =
        {
            new JsonStringEnumConverter(),
            new StronglyTypedIdJsonConverterFactory(),
            new ValueTupleJsonConverterFactory(),
            new PickupAssigneeJsonConverter(),
            new CompletionTargetJsonConverter(),
            new RecurrenceJsonConverter(),
            new EmailVerificationJsonConverter(),
            new WorkDayOverrideJsonConverter()
        }
    };

    public static void AssertMatchesGoldenFile<TEvent>(TEvent @event, string goldenFileName)
    {
        var options = CreateEventSerializerOptions();
        var actual = JsonSerializer.Serialize(@event, options);
        var actualFormatted = Reformat(actual, options);

        var goldenPath = Path.Combine(AppContext.BaseDirectory, "EventShapeTests", "GoldenFiles", goldenFileName);

        if (!File.Exists(goldenPath))
        {
            Assert.Fail(
                $"No golden file at EventShapeTests/GoldenFiles/{goldenFileName}. If this is a deliberate new " +
                $"event, create it with exactly this content:\n{actualFormatted}");
        }

        var expected = Reformat(File.ReadAllText(goldenPath), options);

        Assert.Equal(expected, actualFormatted);
    }

    // The golden files pin the written shape; this pins the read side for events whose shape goes
    // through a hand-written converter, so a converter that writes correctly but reads back a
    // different value still fails. Only for events whose equality is structural (no collections).
    public static void AssertGoldenFileReadsBackAs<TEvent>(TEvent expected, string goldenFileName)
    {
        var goldenPath = Path.Combine(AppContext.BaseDirectory, "EventShapeTests", "GoldenFiles", goldenFileName);
        var actual = JsonSerializer.Deserialize<TEvent>(File.ReadAllText(goldenPath), CreateEventSerializerOptions());

        Assert.Equal(expected, actual);
    }

    private static string Reformat(string json, JsonSerializerOptions options)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions(options) { WriteIndented = true });
    }
}

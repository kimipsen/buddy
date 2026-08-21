using System.Text.Json;
using System.Text.Json.Serialization;

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
    // Mirrors the System.Text.Json configuration every *Feature.cs passes to
    // options.UseSystemTextJsonForSerialization(enumStorage: EnumStorage.AsString, ...) for its
    // Marten store -- enums as their name, strongly-typed ids unwrapped to their raw value.
    public static JsonSerializerOptions CreateEventSerializerOptions() => new()
    {
        Converters =
        {
            new JsonStringEnumConverter(),
            new StronglyTypedIdJsonConverterFactory()
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

    private static string Reformat(string json, JsonSerializerOptions options)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions(options) { WriteIndented = true });
    }
}

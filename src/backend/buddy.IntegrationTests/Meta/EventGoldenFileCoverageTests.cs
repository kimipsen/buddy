using System.Reflection;

using Xunit;

namespace buddy.IntegrationTests.Meta;

// The event-shape counterpart to EndpointCoverageTests: every event type a feature registers
// with Marten (the `EventTypes` array each *Feature.cs passes to options.Events.AddEventTypes)
// must have at least one golden file under EventShapeTests/GoldenFiles/ -- `<EventType>.json`,
// or `<EventType>_<Variant>.json` for an extra shape of the same type. A new event shipped
// without an EventShapeTests [Fact] fails here with its name in the message. No containers
// needed: the registrations are read by reflection, the golden files from the test output.
public sealed class EventGoldenFileCoverageTests
{
    private static readonly string GoldenFilesRoot = Path.Combine(AppContext.BaseDirectory, "EventShapeTests", "GoldenFiles");

    [Fact]
    public void Every_registered_event_type_has_a_golden_file()
    {
        var registered = RegisteredEventTypeNames();
        Assert.NotEmpty(registered);

        var covered = GoldenFileEventNames();

        var missing = registered.Except(covered).Order().ToArray();
        Assert.True(missing.Length == 0, $"These registered event types have no golden file in EventShapeTests/GoldenFiles (add an EventShapeTests [Fact]): {string.Join(", ", missing)}");

        var stale = covered.Except(registered).Order().ToArray();
        Assert.True(stale.Length == 0, $"These golden files don't match any registered event type (renamed or unregistered?): {string.Join(", ", stale)}");
    }

    private static HashSet<string> RegisteredEventTypeNames() =>
        typeof(global::Program).Assembly
            .GetTypes()
            .Where(t => t.Name.EndsWith("Feature", StringComparison.Ordinal))
            .Select(t => t.GetField("EventTypes", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static))
            .OfType<FieldInfo>()
            .SelectMany(f => (Type[])f.GetValue(null)!)
            .Select(t => t.Name)
            .ToHashSet();

    // "EventItemCreated_AllDay.json" covers EventItemCreated.
    private static HashSet<string> GoldenFileEventNames() =>
        Directory.EnumerateFiles(GoldenFilesRoot, "*.json", SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path).Split('_')[0])
            .ToHashSet();
}

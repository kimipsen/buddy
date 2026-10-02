using System.Text.Json.Serialization;

using buddy.Serialization;

namespace buddy.Features.WorkLocations;

// Where an at-location day came from: the repeating pattern, or a per-date override.
public enum WorkDaySource
{
    Pattern,
    Override
}

// One resolved day for ListWorkDays. Location details are inlined so the print sheet needs exactly
// one call per guardian.
public sealed record WorkDay(DateOnly Date, WorkDayStatus Status);

// What a guardian's day resolves to. Unplanned: no override and no pattern entry. Off: an override
// to "not at any location". AtLocation: the pattern's or an override's location. Wire shape
// { "kind": 0 } | { "kind": 1 } | { "kind": 2, "location", "source" }.
[JsonConverter(typeof(WorkDayStatusJsonConverter))]
public abstract record WorkDayStatus
{
    public sealed record Unplanned : WorkDayStatus;

    public sealed record Off : WorkDayStatus;

    public sealed record AtLocation(WorkLocationSummary Location, WorkDaySource Source) : WorkDayStatus;
}

public sealed class WorkDayStatusJsonConverter : KindDiscriminatedJsonConverter<WorkDayStatus>
{
    protected override IReadOnlyDictionary<int, Type> Cases { get; } = new Dictionary<int, Type>
    {
        [0] = typeof(WorkDayStatus.Unplanned),
        [1] = typeof(WorkDayStatus.Off),
        [2] = typeof(WorkDayStatus.AtLocation),
    };
}

public sealed record WorkLocationSummary(Guid Id, string Name, string Icon, string Color, bool IsArchived)
{
    public static WorkLocationSummary From(WorkLocation location) =>
        new(location.Id.Value, location.Name, location.Icon.Value, location.Color.Value, location.IsArchived);
}

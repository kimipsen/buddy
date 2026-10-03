using System.Collections.Immutable;
using System.Text.Json.Serialization;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

public sealed record WorkLocationSchedule(
    WorkLocationScheduleId Id,
    UserId GuardianId,
    ImmutableList<WorkLocation> Locations,
    WorkPattern Pattern,
    // Sparse: only dates with an explicit exception hold a key, like PickupSchedule.Assignments.
    ImmutableDictionary<DateOnly, WorkDayOverride> Overrides)
{
    // What a guardian who has never written anything looks like -- the same state
    // WorkLocationScheduleStarted folds to, so handlers can treat "no stream yet" uniformly.
    public static WorkLocationSchedule Empty(UserId guardianId, DateTimeOffset now) => new(
        WorkLocationScheduleId.ForGuardian(guardianId),
        guardianId,
        [],
        WorkPattern.Empty(DateOnly.FromDateTime(now.UtcDateTime)),
        ImmutableDictionary<DateOnly, WorkDayOverride>.Empty);

    public WorkLocation? FindLocation(WorkLocationId id) => Locations.FirstOrDefault(l => l.Id == id);

    public WorkLocation? FindActiveLocation(WorkLocationId id) => FindLocation(id) is { IsArchived: false } location ? location : null;

    // Derived, so kept out of the persisted snapshot JSON.
    [JsonIgnore]
    public IEnumerable<WorkLocation> ActiveLocations => Locations.Where(l => !l.IsArchived);

    private WorkLocationSchedule WithLocation(WorkLocationId id, Func<WorkLocation, WorkLocation> change) =>
        this with { Locations = Locations.Select(l => l.Id == id ? change(l) : l).ToImmutableList() };

    public static WorkLocationSchedule? Rehydrate(IEnumerable<WorkLocationEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static WorkLocationSchedule Replay(IEnumerable<WorkLocationEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so
    // WorkLocationScheduleSnapshotProjection can drive the same logic one Marten-delivered event at
    // a time instead of duplicating this switch. Deliberately not named Apply/Create -- those names
    // are a convention JasperFx's projection source generator scans for on any type used as a
    // projection document (see Question 4/5 in docs/backend/analysis/event-stream-snapshots.md).
    public static WorkLocationSchedule Start(WorkLocationEvent @event) => @event switch
    {
        WorkLocationScheduleStarted started => Empty(started.GuardianId, started.OccurredAt),
        _ => throw EventReplay.NotAStartEvent(nameof(WorkLocationSchedule), @event.EventType)
    };

    public static WorkLocationSchedule Advance(WorkLocationSchedule schedule, WorkLocationEvent @event) => @event switch
    {
        WorkLocationAdded added => schedule with
        {
            Locations = schedule.Locations.Add(new WorkLocation(added.LocationId, added.Name, added.Icon, added.Color))
        },
        WorkLocationDetailsChanged changed => schedule.WithLocation(
            changed.LocationId, l => l with { Name = changed.After.Name, Icon = changed.After.Icon, Color = changed.After.Color }),
        WorkLocationArchived archived => schedule.WithLocation(archived.LocationId, l => l with { IsArchived = true }),
        WorkPatternReplaced replaced => schedule with { Pattern = replaced.After },
        WorkLocationOverridden overridden => schedule with
        {
            Overrides = schedule.Overrides.SetItem(overridden.Date, overridden.Override)
        },
        WorkLocationOverrideCleared cleared => schedule with
        {
            Overrides = schedule.Overrides.Remove(cleared.Date)
        },
        WorkLocationScheduleStarted => throw EventReplay.AlreadyStarted(nameof(WorkLocationSchedule), @event.EventType)
    };
}

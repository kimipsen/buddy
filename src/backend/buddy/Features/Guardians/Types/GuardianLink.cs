using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record GuardianLink(
    GuardianLinkId Id,
    UserId ChildId,
    UserId GuardianId,
    GuardianKind Kind,
    bool IsRevoked = false)
{
    public static GuardianLink? Rehydrate(IEnumerable<GuardianEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static GuardianLink Replay(IEnumerable<GuardianEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so
    // GuardianLinkSnapshotProjection can drive the same logic one Marten-delivered event at a time
    // instead of duplicating this switch. Deliberately not named Apply/Create -- those names are a
    // convention JasperFx's projection source generator scans for on any type used as a projection
    // document, and GuardianLink is that document (see Question 4/5 in
    // docs/backend/analysis/event-stream-snapshots.md).
    public static GuardianLink Start(GuardianEvent @event) => @event switch
    {
        GuardianLinked linked => new GuardianLink(linked.GuardianLinkId, linked.ChildId, linked.GuardianId, linked.Kind),
        _ => throw EventReplay.NotAStartEvent(nameof(GuardianLink), @event.EventType)
    };

    public static GuardianLink Advance(GuardianLink link, GuardianEvent @event) => @event switch
    {
        GuardianKindChanged changed => link with { Kind = changed.After },
        GuardianRevoked => link with { IsRevoked = true },
        GuardianLinked => throw EventReplay.AlreadyStarted(nameof(GuardianLink), @event.EventType)
    };
}

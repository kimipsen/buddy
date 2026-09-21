using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record GuardianLink(
    GuardianLinkId Id,
    UserId ChildId,
    UserId GuardianId,
    GuardianKind Kind,
    bool IsRevoked = false)
{
    public static GuardianLink? Rehydrate(IEnumerable<GuardianEvent> events) => events.Aggregate((GuardianLink?)null, Fold);

    // Single-event step, split out from Rehydrate so GuardianLinkSnapshotProjection can drive the
    // same logic one Marten-delivered event at a time instead of duplicating this switch.
    // Deliberately not named Apply/Create -- those names are a convention JasperFx's projection
    // source generator scans for on any type used as a projection document, and GuardianLink is
    // that document (see Question 4/5 in docs/backend/analysis/event-stream-snapshots.md).
    public static GuardianLink? Fold(GuardianLink? link, GuardianEvent @event) => @event switch
    {
        GuardianLinked linked => new GuardianLink(linked.GuardianLinkId, linked.ChildId, linked.GuardianId, linked.Kind),
        GuardianKindChanged changed => link! with { Kind = changed.After },
        GuardianRevoked => link! with { IsRevoked = true },
        _ => link
    };
}

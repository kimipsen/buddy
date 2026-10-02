using Marten.Events.Aggregation;

namespace buddy.Features.Guardians;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct GuardianLinkId(Guid Value)" -- as a document's Id.
// GuardianLinkId here is a sealed record (a class), which Marten's DocumentMapping rejects with
// "Could not determine an 'id/Id' field or property". So the snapshot document can't be
// GuardianLink itself; this thin wrapper carries the plain Guid Marten needs alongside the actual
// GuardianLink value. GuardianLinkId stays untouched everywhere else in the codebase -- this
// wrapper exists purely at the snapshot-storage boundary. See
// docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record GuardianLinkSnapshot(Guid Id, GuardianLink GuardianLink);

// Inline snapshot of GuardianLink, maintained by Marten in the same transaction as every event
// append (see UsersFeature.AddUsersFeature: options.Projections.Register(new
// GuardianLinkSnapshotProjection(), ...)). GuardianLink's stream lives in the same Marten
// store/schema ("users") as User -- see MartenGuardianLinkEventStore -- so this projection is
// registered alongside UserSnapshotProjection there, not in a Guardians-specific feature file.
// Stored in the shared "snapshots" schema, never the "users" event schema -- it is derived,
// rebuildable state, not a second source of truth.
public sealed class GuardianLinkSnapshotProjection : SingleStreamProjection<GuardianLinkSnapshot, Guid>
{
    public static GuardianLinkSnapshot Create(GuardianLinked linked) =>
        new(linked.GuardianLinkId.Value, GuardianLink.Start(GuardianEvent.FromPayload(linked)));

    public GuardianLinkSnapshot Apply(GuardianLinkSnapshot current, GuardianKindChanged changed) =>
        current with { GuardianLink = GuardianLink.Advance(current.GuardianLink, GuardianEvent.FromPayload(changed)) };

    public GuardianLinkSnapshot Apply(GuardianLinkSnapshot current, GuardianRevoked revoked) =>
        current with { GuardianLink = GuardianLink.Advance(current.GuardianLink, GuardianEvent.FromPayload(revoked)) };
}

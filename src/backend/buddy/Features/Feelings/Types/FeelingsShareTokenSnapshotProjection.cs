using Marten.Events.Aggregation;

namespace buddy.Features.Feelings;

public sealed record FeelingsShareTokenSnapshot(Guid Id, FeelingsShareToken FeelingsShareToken);

public sealed class FeelingsShareTokenSnapshotProjection : SingleStreamProjection<FeelingsShareTokenSnapshot, Guid>
{
    public static FeelingsShareTokenSnapshot Create(FeelingsShareTokenCreated created) =>
        new(created.Id.Value, FeelingsShareToken.Start(FeelingsShareTokenEvent.FromPayload(created)));

    public FeelingsShareTokenSnapshot Apply(FeelingsShareTokenSnapshot current, FeelingsShareTokenRevoked revoked) =>
        current with { FeelingsShareToken = FeelingsShareToken.Advance(current.FeelingsShareToken, FeelingsShareTokenEvent.FromPayload(revoked)) };
}

// Looked up by TokenHash when an anonymous reader opens a share link, and by ChildId to list a
// child's links. Written on FeelingsShareTokenCreated and flipped on FeelingsShareTokenRevoked in
// the same session as the event append, like SleepDiaryShareTokenDocument.
public sealed record FeelingsShareTokenDocument(
    Guid Id,
    Guid ChildId,
    string TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool IsRevoked);

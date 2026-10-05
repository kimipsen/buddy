using Marten.Events.Aggregation;

namespace buddy.Features.SleepDiaries;

public sealed record SleepDiaryShareTokenSnapshot(Guid Id, SleepDiaryShareToken SleepDiaryShareToken);

public sealed class SleepDiaryShareTokenSnapshotProjection : SingleStreamProjection<SleepDiaryShareTokenSnapshot, Guid>
{
    public static SleepDiaryShareTokenSnapshot Create(SleepDiaryShareTokenCreated created) =>
        new(created.Id.Value, SleepDiaryShareToken.Start(SleepDiaryShareTokenEvent.FromPayload(created)));

    public SleepDiaryShareTokenSnapshot Apply(SleepDiaryShareTokenSnapshot current, SleepDiaryShareTokenRevoked revoked) =>
        current with { SleepDiaryShareToken = SleepDiaryShareToken.Advance(current.SleepDiaryShareToken, SleepDiaryShareTokenEvent.FromPayload(revoked)) };
}

// Looked up by TokenHash when an anonymous reader opens a share link, and by ChildId to list a
// child's links. Written on SleepDiaryShareTokenCreated and flipped on SleepDiaryShareTokenRevoked
// in the same session as the event append, like MedicineIndexDocument.
public sealed record SleepDiaryShareTokenDocument(
    Guid Id,
    Guid ChildId,
    string TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool IsRevoked);

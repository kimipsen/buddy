using Marten.Events.Aggregation;

namespace buddy.Features.VisitSummaries;

// Thin wrapper: Marten can't use the sealed-record VisitSummaryId as a document Id. See
// docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record VisitSummarySnapshot(Guid Id, VisitSummary VisitSummary);

public sealed class VisitSummarySnapshotProjection : SingleStreamProjection<VisitSummarySnapshot, Guid>
{
    public static VisitSummarySnapshot Create(VisitSummaryCreated created) =>
        new(created.Id.Value, VisitSummary.Start(VisitSummaryEvent.FromPayload(created)));

    public VisitSummarySnapshot Apply(VisitSummarySnapshot current, VisitSummaryUpdated updated) =>
        current with { VisitSummary = VisitSummary.Advance(current.VisitSummary, VisitSummaryEvent.FromPayload(updated)) };

    public VisitSummarySnapshot Apply(VisitSummarySnapshot current, VisitSummaryDeleted deleted) =>
        current with { VisitSummary = VisitSummary.Advance(current.VisitSummary, VisitSummaryEvent.FromPayload(deleted)) };

    public VisitSummarySnapshot Apply(VisitSummarySnapshot current, VisitSummaryShareLinkCreated created) =>
        current with { VisitSummary = VisitSummary.Advance(current.VisitSummary, VisitSummaryEvent.FromPayload(created)) };

    public VisitSummarySnapshot Apply(VisitSummarySnapshot current, VisitSummaryShareLinkRevoked revoked) =>
        current with { VisitSummary = VisitSummary.Advance(current.VisitSummary, VisitSummaryEvent.FromPayload(revoked)) };
}

// Lists a child's summaries without reading every stream. Written in the same session as the
// event append; a deleted summary keeps its document (IsDeleted) so erasure can still find it.
public sealed record VisitSummaryIndexDocument(Guid Id, Guid ChildId, bool IsDeleted);

// Looked up by TokenHash when an anonymous reader opens a link. Kept in step with the stream in the
// same transaction, so a revoked link or deleted summary stops resolving the moment the call returns.
public sealed record VisitSummaryShareLinkDocument(
    Guid Id,
    Guid SummaryId,
    Guid ChildId,
    string TokenHash,
    DateTimeOffset? ExpiresAt,
    bool IsRevoked);

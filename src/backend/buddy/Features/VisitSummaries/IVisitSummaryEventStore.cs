using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

public interface IVisitSummaryEventStore
{
    Task<IReadOnlyCollection<VisitSummaryEvent>> ReadAsync(VisitSummaryId id, CancellationToken cancellationToken);

    Task<VisitSummary?> FindSnapshotAsync(VisitSummaryId id, CancellationToken cancellationToken);

    Task CreateAsync(VisitSummaryId id, IReadOnlyCollection<VisitSummaryEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(VisitSummaryId id, IReadOnlyCollection<VisitSummaryEvent> events, CancellationToken cancellationToken);

    // Deleted summaries included -- erasure needs them; callers filter.
    Task<IReadOnlyCollection<VisitSummaryIndexDocument>> ListForChildAsync(UserId childId, CancellationToken cancellationToken);

    Task<VisitSummaryShareLinkDocument?> FindLinkByHashAsync(string tokenHash, CancellationToken cancellationToken);
}

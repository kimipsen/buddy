using buddy.Common;
using buddy.Common.Security;

namespace buddy.Features.VisitSummaries;

public static class GetSharedVisitSummaryHandler
{
    public static async Task<Result<SharedVisitSummaryResponse>> Handle(
        GetSharedVisitSummary query,
        IVisitSummaryEventStore summaries,
        VisitSummaryReports reports,
        TimeProvider time,
        ILogger<GetSharedVisitSummary> logger,
        CancellationToken cancellationToken)
    {
        var link = await summaries.FindLinkByHashAsync(ShareSecret.Hash(query.Token), cancellationToken);
        var now = time.GetUtcNow();

        // Unknown, revoked, expired and deleted-summary all look the same to an anonymous reader.
        if (link is null || link.IsRevoked || link.ExpiresAt <= now
            || await summaries.FindSnapshotAsync(new VisitSummaryId(link.SummaryId), cancellationToken) is not { IsDeleted: false } summary
            || summary.FindLink(new VisitSummaryShareLinkId(link.Id)) is not { } live
            || !live.IsLive(now)
            || await reports.BuildAsync(summary, cancellationToken) is not { } report)
        {
            return new Result<SharedVisitSummaryResponse>.NotFound();
        }

        logger.SharedReportViewed(summary.Id.Value, summary.ChildId.Value, link.Id);

        return new Result<SharedVisitSummaryResponse>.Success(new SharedVisitSummaryResponse(report, live.ExpiresAt));
    }
}

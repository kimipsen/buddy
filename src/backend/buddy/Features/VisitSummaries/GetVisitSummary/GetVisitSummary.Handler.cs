using buddy.Common;
using buddy.Common.Observability;
using buddy.Features.Guardians;

namespace buddy.Features.VisitSummaries;

public static class GetVisitSummaryHandler
{
    // The summary and the report exactly as a doctor with a link would see it.
    public static async Task<Result<VisitSummaryWithReportResponse>> Handle(
        GetVisitSummary query,
        IVisitSummaryEventStore summaries,
        IGuardianLinkEventStore guardians,
        VisitSummaryReports reports,
        TimeProvider time,
        ILogger<GetVisitSummary> logger,
        CancellationToken cancellationToken)
    {
        var access = await VisitSummaryAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != VisitSummaryAccess.Allowed)
        {
            return access.ToDeniedResult<VisitSummaryWithReportResponse>();
        }

        if (await summaries.FindSnapshotAsync(query.SummaryId, cancellationToken) is not { IsDeleted: false } summary
            || summary.ChildId != query.ChildId
            || await reports.BuildAsync(summary, cancellationToken) is not { } report)
        {
            return new Result<VisitSummaryWithReportResponse>.NotFound();
        }

        logger.ReportRead(summary.Id.Value, summary.ChildId.Value, query.UserId.Value, HealthDataAccessPath.Guardian);

        return new Result<VisitSummaryWithReportResponse>.Success(
            new VisitSummaryWithReportResponse(VisitSummaryResponse.FromSummary(summary, time.GetUtcNow()), report));
    }
}

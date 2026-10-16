using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.VisitSummaries;

public static class ListVisitSummariesHandler
{
    // The summaries only -- no report, so the list never reads any health data.
    public static async Task<Result<IReadOnlyList<VisitSummaryResponse>>> Handle(
        ListVisitSummaries query,
        IVisitSummaryEventStore summaries,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await VisitSummaryAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != VisitSummaryAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyList<VisitSummaryResponse>>();
        }

        var now = time.GetUtcNow();
        var loaded = (await summaries.ListSnapshotsForChildAsync(query.ChildId, cancellationToken)).Where(s => !s.IsDeleted);

        // Newest visit first; a summary with no visit date sorts by its period's end.
        IReadOnlyList<VisitSummaryResponse> ordered =
        [
            .. loaded
                .OrderByDescending(s => s.Details.VisitDate ?? s.Details.To)
                .ThenByDescending(s => s.LastModifiedAt)
                .Select(s => VisitSummaryResponse.FromSummary(s, now))
        ];

        return new Result<IReadOnlyList<VisitSummaryResponse>>.Success(ordered);
    }
}

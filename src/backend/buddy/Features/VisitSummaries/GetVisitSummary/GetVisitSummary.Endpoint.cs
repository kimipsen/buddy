using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.VisitSummaries;

public static class GetVisitSummaryEndpoint
{
    public static RouteGroupBuilder MapGetVisitSummary(this RouteGroupBuilder visitSummaries)
    {
        visitSummaries.MapGet("/children/{childId:guid}/{summaryId:guid}", async Task<Results<Ok<VisitSummaryWithReportResponse>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid summaryId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = GetVisitSummary.FromClaims(principal, new UserId(childId), new VisitSummaryId(summaryId));
            var result = await bus.InvokeAsync<Result<VisitSummaryWithReportResponse>>(query, cancellationToken);

            return result switch
            {
                Result<VisitSummaryWithReportResponse>.Success(var summary) => TypedResults.Ok(summary),
                Result<VisitSummaryWithReportResponse>.NotFound => TypedResults.NotFound(),
                // No Forbidden tier and no validation rules -- both unreachable.
                Result<VisitSummaryWithReportResponse>.Forbidden => TypedResults.NotFound(),
                Result<VisitSummaryWithReportResponse>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetVisitSummary");

        return visitSummaries;
    }
}

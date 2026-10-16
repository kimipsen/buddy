using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.VisitSummaries;

public static class ListVisitSummariesEndpoint
{
    public static RouteGroupBuilder MapListVisitSummaries(this RouteGroupBuilder visitSummaries)
    {
        visitSummaries.MapGet("/children/{childId:guid}", async Task<Results<Ok<IReadOnlyList<VisitSummaryResponse>>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListVisitSummaries.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<IReadOnlyList<VisitSummaryResponse>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyList<VisitSummaryResponse>>.Success(var summaries) => TypedResults.Ok(summaries),
                Result<IReadOnlyList<VisitSummaryResponse>>.NotFound => TypedResults.NotFound(),
                // No Forbidden tier and no validation rules -- both unreachable.
                Result<IReadOnlyList<VisitSummaryResponse>>.Forbidden => TypedResults.NotFound(),
                Result<IReadOnlyList<VisitSummaryResponse>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListVisitSummaries");

        return visitSummaries;
    }
}

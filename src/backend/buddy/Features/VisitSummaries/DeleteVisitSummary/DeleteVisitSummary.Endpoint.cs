using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.VisitSummaries;

public static class DeleteVisitSummaryEndpoint
{
    public static RouteGroupBuilder MapDeleteVisitSummary(this RouteGroupBuilder visitSummaries)
    {
        visitSummaries.MapDelete("/children/{childId:guid}/{summaryId:guid}", async Task<Results<NoContent, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid summaryId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = DeleteVisitSummary.FromClaims(principal, new UserId(childId), new VisitSummaryId(summaryId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // No Forbidden tier and no validation rules -- both unreachable.
                Result<Unit>.Forbidden => TypedResults.NotFound(),
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("DeleteVisitSummary");

        return visitSummaries;
    }
}

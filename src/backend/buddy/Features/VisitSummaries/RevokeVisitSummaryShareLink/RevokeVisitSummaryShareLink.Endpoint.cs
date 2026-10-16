using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.VisitSummaries;

public static class RevokeVisitSummaryShareLinkEndpoint
{
    public static RouteGroupBuilder MapRevokeVisitSummaryShareLink(this RouteGroupBuilder visitSummaries)
    {
        visitSummaries.MapDelete("/children/{childId:guid}/{summaryId:guid}/share-links/{linkId:guid}", async Task<Results<NoContent, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid summaryId,
            Guid linkId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevokeVisitSummaryShareLink.FromClaims(
                principal, new UserId(childId), new VisitSummaryId(summaryId), new VisitSummaryShareLinkId(linkId));
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
        .WithName("RevokeVisitSummaryShareLink");

        return visitSummaries;
    }
}

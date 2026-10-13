using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class ListFeelingsShareLinksEndpoint
{
    public static RouteGroupBuilder MapListFeelingsShareLinks(this RouteGroupBuilder feelings)
    {
        feelings.MapGet("/children/{childId:guid}/share-links", async Task<Results<Ok<IReadOnlyCollection<FeelingsShareLinkSummary>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListFeelingsShareLinks.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<FeelingsShareLinkSummary>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<FeelingsShareLinkSummary>>.Success(var links) => TypedResults.Ok(links),
                Result<IReadOnlyCollection<FeelingsShareLinkSummary>>.NotFound => TypedResults.NotFound(),
                Result<IReadOnlyCollection<FeelingsShareLinkSummary>>.Forbidden => TypedResults.Forbid(),
                // No validation rules -- unreachable.
                Result<IReadOnlyCollection<FeelingsShareLinkSummary>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListFeelingsShareLinks");

        return feelings;
    }
}

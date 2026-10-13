using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class RevokeFeelingsShareLinkEndpoint
{
    public static RouteGroupBuilder MapRevokeFeelingsShareLink(this RouteGroupBuilder feelings)
    {
        feelings.MapDelete("/children/{childId:guid}/share-links/{shareLinkId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid shareLinkId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevokeFeelingsShareLink.FromClaims(principal, new UserId(childId), new FeelingsShareTokenId(shareLinkId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                // No validation rules -- unreachable.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RevokeFeelingsShareLink");

        return feelings;
    }
}

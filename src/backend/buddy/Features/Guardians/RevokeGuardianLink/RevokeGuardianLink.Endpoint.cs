using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class RevokeGuardianLinkEndpoint
{
    public static RouteGroupBuilder MapRevokeGuardianLink(this RouteGroupBuilder children)
    {
        children.MapDelete("/{childId:guid}/guardian-link", async Task<Results<NoContent, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevokeGuardianLink.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // RevokeGuardianLinkHandler never produces Forbidden or Validation -- collapsed to
                // NotFound since this route declares no other status for them.
                Result<Unit>.Forbidden => TypedResults.NotFound(),
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RevokeGuardianLink");

        return children;
    }
}

using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class AcceptGroupInviteEndpoint
{
    public static RouteGroupBuilder MapAcceptGroupInvite(this RouteGroupBuilder invites)
    {
        invites.MapPost("/{token}/accept", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            string token,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = AcceptGroupInvite.FromClaims(principal, token);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // AcceptGroupInviteHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .RequireAuthorization()
        .WithName("AcceptGroupInvite");

        return invites;
    }
}

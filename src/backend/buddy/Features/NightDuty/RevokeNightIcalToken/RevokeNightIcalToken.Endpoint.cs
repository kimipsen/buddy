using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.NightDuty;

public static class RevokeNightIcalTokenEndpoint
{
    public static RouteGroupBuilder MapRevokeNightIcalToken(this RouteGroupBuilder nightDuty)
    {
        nightDuty.MapDelete("/children/{childId:guid}/ical-tokens/{tokenId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid tokenId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevokeNightIcalToken.FromClaims(principal, new UserId(childId), new IcalTokenId(tokenId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                // The command has no input to validate -- collapsed to NotFound since this route
                // declares no 400.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RevokeNightIcalToken");

        return nightDuty;
    }
}

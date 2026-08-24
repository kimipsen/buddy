using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class RevokeGuardianInviteEndpoint
{
    public static RouteGroupBuilder MapRevokeGuardianInvite(this RouteGroupBuilder children)
    {
        children.MapDelete("/{childId:guid}/guardian-invites/{inviteId:guid}", async Task<Results<NoContent, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid inviteId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevokeGuardianInvite.FromClaims(principal, new UserId(childId), inviteId);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // RevokeGuardianInviteHandler never produces Forbidden/Validation -- collapsed
                // to NotFound since this route declares no other status for them.
                Result<Unit>.Forbidden => TypedResults.NotFound(),
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RevokeGuardianInvite");

        return children;
    }
}

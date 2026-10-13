using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class RemoveFeelingCheckInEndpoint
{
    public static RouteGroupBuilder MapRemoveFeelingCheckIn(this RouteGroupBuilder feelings)
    {
        feelings.MapDelete("/children/{childId:guid}/check-ins/{checkInId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid checkInId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RemoveFeelingCheckIn.FromClaims(principal, new UserId(childId), new FeelingCheckInId(checkInId));
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
        .WithName("RemoveFeelingCheckIn");

        return feelings;
    }
}

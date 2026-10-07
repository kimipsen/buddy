using System.Diagnostics;
using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class DeleteCurrentUserEndpoint
{
    public static RouteGroupBuilder MapDeleteCurrentUser(this RouteGroupBuilder users)
    {
        users.MapDelete("/me", async Task<Results<NoContent, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<Unit>>(DeleteUser.FromClaims(principal), cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound or Result<Unit>.Validation => throw new UnreachableException("DeleteUser only succeeds or forbids."),
            };
        })
        .WithName("DeleteCurrentUser");

        return users;
    }
}

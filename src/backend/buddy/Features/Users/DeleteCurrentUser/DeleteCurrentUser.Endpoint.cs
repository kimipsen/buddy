using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class DeleteCurrentUserEndpoint
{
    public static RouteGroupBuilder MapDeleteCurrentUser(this RouteGroupBuilder users)
    {
        users.MapDelete("/me", async Task<NoContent> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            await bus.InvokeAsync(DeleteUser.FromClaims(principal), cancellationToken);

            return TypedResults.NoContent();
        })
        .WithName("DeleteCurrentUser");

        return users;
    }
}

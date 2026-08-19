using System.Security.Claims;
using Wolverine;

namespace buddy.Features.Users;

public static class DeleteCurrentUserEndpoint
{
    public static RouteGroupBuilder MapDeleteCurrentUser(this RouteGroupBuilder users)
    {
        users.MapDelete("/me", async (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            await bus.InvokeAsync(DeleteUser.FromClaims(principal), cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeleteCurrentUser");

        return users;
    }
}

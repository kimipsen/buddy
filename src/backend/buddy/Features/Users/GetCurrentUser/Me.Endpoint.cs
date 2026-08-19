using System.Security.Claims;
using Wolverine;

namespace buddy.Features.Users;

public static class GetCurrentUserEndpoint
{
    public static RouteGroupBuilder MapGetCurrentUser(this RouteGroupBuilder users)
    {
        users.MapGet("/me", async (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var user = await bus.InvokeAsync<User>(GetOrCreateUser.FromClaims(principal), cancellationToken);

            if (user.IsDeleted)
            {
                return Results.NotFound();
            }

            return Results.Ok(new UserResponse(
                user.Id,
                user.KeycloakSubject,
                user.Email,
                user.UserName,
                user.Name));
        })
        .WithName("GetCurrentUser");

        return users;
    }
}

public sealed record UserResponse(
    UserId Id,
    KeycloakSubject KeycloakSubject,
    Email Email,
    string? UserName,
    Name Name);

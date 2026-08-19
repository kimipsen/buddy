using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class GetCurrentUserEndpoint
{
    public static RouteGroupBuilder MapGetCurrentUser(this RouteGroupBuilder users)
    {
        users.MapGet("/me", async Task<Results<Ok<UserResponse>, NotFound>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var user = await bus.InvokeAsync<User>(GetOrCreateUser.FromClaims(principal), cancellationToken);

            if (user.IsDeleted)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(UserResponse.FromUser(user));
        })
        .WithName("GetCurrentUser");

        return users;
    }
}

public sealed record UserResponse(
    UserId Id,
    Email Email,
    string? UserName,
    Name Name)
{
    public static UserResponse FromUser(User user) => new(
        user.Id,
        user.Email,
        user.UserName,
        user.Name);
};

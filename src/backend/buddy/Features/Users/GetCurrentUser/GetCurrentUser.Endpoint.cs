using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class GetCurrentUserEndpoint
{
    public static RouteGroupBuilder MapGetCurrentUser(this RouteGroupBuilder users)
    {
        users.MapGet("/me", async Task<Results<Ok<UserResponse>, NotFound>> (
            ClaimsPrincipal principal,
            HttpContext httpContext,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var acceptLanguage = httpContext.Request.Headers["Accept-Language"].ToString();
            var result = await bus.InvokeAsync<Result<User>>(GetOrCreateUser.FromClaims(principal, acceptLanguage), cancellationToken);

            return result switch
            {
                Result<User>.Success(var user) => TypedResults.Ok(UserResponse.FromUser(user)),
                Result<User>.NotFound => TypedResults.NotFound(),
                // GetOrCreateUserHandler never produces Forbidden or Validation -- collapsed to
                // NotFound since this route declares no other status for them.
                Result<User>.Forbidden => TypedResults.NotFound(),
                Result<User>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetCurrentUser");

        return users;
    }
}

public sealed record UserResponse(
    UserId Id,
    Email Email,
    string? UserName,
    Name Name,
    TimeZoneId TimeZoneId,
    Language Language)
{
    public static UserResponse FromUser(User user) => new(
        user.Id,
        user.Email,
        user.UserName,
        user.Name,
        user.ResolvedTimeZoneId,
        user.ResolvedLanguage);
};

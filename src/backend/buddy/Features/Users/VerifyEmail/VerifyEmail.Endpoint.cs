using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class VerifyEmailEndpoint
{
    public static RouteGroupBuilder MapVerifyCurrentEmail(this RouteGroupBuilder users)
    {
        users.MapPost("/me/email/verify", async Task<Results<Ok<UserResponse>, BadRequest<string>, NotFound>> (
            ClaimsPrincipal principal,
            VerifyEmailRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = VerifyEmail.FromClaims(principal, request.Token);
            var result = await bus.InvokeAsync<Result<User>>(command, cancellationToken);

            return result switch
            {
                Result<User>.Success(var user) => TypedResults.Ok(UserResponse.FromUser(user)),
                Result<User>.Validation(var message) => TypedResults.BadRequest(message),
                Result<User>.NotFound => TypedResults.NotFound(),
                // VerifyEmailHandler never produces Forbidden -- collapsed to NotFound since this
                // route declares no other status for it.
                Result<User>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("VerifyCurrentEmail");

        return users;
    }
}

public sealed record VerifyEmailRequest(string Token);

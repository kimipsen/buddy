using System.Security.Claims;

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
            var outcome = await bus.InvokeAsync<VerifyEmailOutcome>(command, cancellationToken);

            return outcome.Result switch
            {
                VerifyEmailResult.UserNotFound => TypedResults.NotFound(),
                VerifyEmailResult.InvalidToken => TypedResults.BadRequest("The verification token is invalid."),
                VerifyEmailResult.Expired => TypedResults.BadRequest("The verification token has expired."),
                _ => TypedResults.Ok(UserResponse.FromUser(outcome.User!))
            };
        })
        .WithName("VerifyCurrentEmail");

        return users;
    }
}

public sealed record VerifyEmailRequest(string Token);

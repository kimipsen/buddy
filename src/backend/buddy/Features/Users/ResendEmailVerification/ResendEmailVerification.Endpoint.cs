using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class ResendEmailVerificationEndpoint
{
    public static RouteGroupBuilder MapResendCurrentEmailVerification(this RouteGroupBuilder users)
    {
        users.MapPost("/me/email/verify/resend", async Task<Results<NoContent, Conflict<string>, NotFound>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ResendEmailVerification.FromClaims(principal);
            var result = await bus.InvokeAsync<ResendEmailVerificationResult>(command, cancellationToken);

            return result switch
            {
                ResendEmailVerificationResult.UserNotFound => TypedResults.NotFound(),
                ResendEmailVerificationResult.AlreadyVerified => TypedResults.NoContent(),
                ResendEmailVerificationResult.TooManyRequests => TypedResults.Conflict("A verification email was already sent recently."),
                _ => TypedResults.NoContent()
            };
        })
        .WithName("ResendCurrentUserEmailVerification");

        return users;
    }
}

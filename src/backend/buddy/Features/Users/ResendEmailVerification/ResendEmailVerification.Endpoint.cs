using System.Security.Claims;

using buddy.Common;
using buddy.Common.RateLimiting;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class ResendEmailVerificationEndpoint
{
    public static RouteGroupBuilder MapResendCurrentEmailVerification(this RouteGroupBuilder users)
    {
        users.MapPost("/me/email/verify/resend", async Task<Results<NoContent, Conflict<ErrorEnvelope>, NotFound>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = ResendEmailVerification.FromClaims(principal);
            var result = await bus.InvokeAsync<ResendEmailVerificationOutcome>(command, cancellationToken);

            return result switch
            {
                ResendEmailVerificationOutcome.Sent => TypedResults.NoContent(),
                ResendEmailVerificationOutcome.AlreadyVerified => TypedResults.NoContent(),
                ResendEmailVerificationOutcome.NotFound => TypedResults.NotFound(),
                ResendCooldownActive cooldown => cooldown.ToConflict(httpContext),
            };
        })
        .WithName("ResendCurrentUserEmailVerification");

        return users;
    }
}

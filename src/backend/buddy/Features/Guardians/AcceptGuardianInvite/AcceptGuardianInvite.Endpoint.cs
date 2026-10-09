using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class AcceptGuardianInviteEndpoint
{
    public static RouteGroupBuilder MapAcceptGuardianInvite(this RouteGroupBuilder invites)
    {
        invites.MapPost("/{token}/accept", async Task<Results<NoContent, NotFound, ForbidHttpResult, JsonHttpResult<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            string token,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = AcceptGuardianInvite.FromClaims(principal, token);
            var result = await bus.InvokeAsync<AcceptGuardianInviteOutcome>(command, cancellationToken);

            return result switch
            {
                AcceptGuardianInviteOutcome.Success => TypedResults.NoContent(),
                AcceptGuardianInviteOutcome.Forbidden => TypedResults.Forbid(),
                AcceptGuardianInviteOutcome.NotFound => TypedResults.NotFound(),
                EmailNotVerified notVerified => notVerified.ToForbidden(httpContext),
            };
        })
        .RequireAuthorization()
        // JsonHttpResult adds no response metadata; the plain 403 (another address) has no body.
        .Produces<ErrorEnvelope>(StatusCodes.Status403Forbidden)
        .ProducesErrorCode(StatusCodes.Status403Forbidden, EmailNotVerifiedExtensions.ErrorCode)
        .WithName("AcceptGuardianInvite");

        return invites;
    }
}

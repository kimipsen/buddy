using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class AcceptGroupInviteEndpoint
{
    public static RouteGroupBuilder MapAcceptGroupInvite(this RouteGroupBuilder invites)
    {
        invites.MapPost("/{token}/accept", async Task<Results<NoContent, NotFound, ForbidHttpResult, JsonHttpResult<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            string token,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = AcceptGroupInvite.FromClaims(principal, token);
            var result = await bus.InvokeAsync<AcceptGroupInviteOutcome>(command, cancellationToken);

            return result switch
            {
                AcceptGroupInviteOutcome.Success => TypedResults.NoContent(),
                AcceptGroupInviteOutcome.Forbidden => TypedResults.Forbid(),
                AcceptGroupInviteOutcome.NotFound => TypedResults.NotFound(),
                EmailNotVerified notVerified => notVerified.ToForbidden(httpContext),
            };
        })
        .RequireAuthorization()
        // JsonHttpResult adds no response metadata; the plain 403 (another address) has no body.
        .Produces<ErrorEnvelope>(StatusCodes.Status403Forbidden)
        .ProducesErrorCode(StatusCodes.Status403Forbidden, EmailNotVerifiedExtensions.ErrorCode)
        .WithName("AcceptGroupInvite");

        return invites;
    }
}

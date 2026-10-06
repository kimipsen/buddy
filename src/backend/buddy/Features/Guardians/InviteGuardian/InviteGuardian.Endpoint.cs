using System.Security.Claims;

using buddy.Common;
using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class InviteGuardianEndpoint
{
    public static RouteGroupBuilder MapInviteGuardian(this RouteGroupBuilder children)
    {
        children.MapPost("/{childId:guid}/guardian-invites", async Task<Results<Ok<GuardianInviteResponse>, NotFound, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            InviteGuardianRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = InviteGuardian.FromClaims(principal, new UserId(childId), request.Email, request.Kind);
            var result = await bus.InvokeAsync<InviteGuardianOutcome>(command, cancellationToken);

            return result switch
            {
                InviteGuardianOutcome.Success(var invite) => TypedResults.Ok(GuardianInviteResponse.FromSummary(invite)),
                InviteGuardianOutcome.NotFound => TypedResults.NotFound(),
                ResendCooldownActive cooldown => cooldown.ToConflict(httpContext),
            };
        })
        .RequireRateLimiting(RateLimitingFeature.OutboundEmailPolicy)
        .WithName("InviteGuardian");

        return children;
    }
}

public sealed record InviteGuardianRequest(string Email, GuardianKind Kind);

public sealed record GuardianInviteResponse(Guid Id, string Email, GuardianKind Kind, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt)
{
    public static GuardianInviteResponse FromSummary(GuardianInviteSummary summary) =>
        new(summary.Id, summary.Email, summary.Kind, summary.InvitedAt, summary.ExpiresAt);
}

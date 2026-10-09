using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;
using buddy.Common.RateLimiting;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class InviteToGroupEndpoint
{
    public static RouteGroupBuilder MapInviteToGroup(this RouteGroupBuilder groups)
    {
        groups.MapPost("/{groupId:guid}/invites", async Task<Results<Ok<SentGroupInviteResponse>, NotFound, ForbidHttpResult, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            InviteToGroupRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = InviteToGroup.FromClaims(principal, new GroupId(groupId), request.Email, request.Role);
            var result = await bus.InvokeAsync<InviteToGroupOutcome>(command, cancellationToken);

            return result switch
            {
                InviteToGroupOutcome.Success(var invite) => TypedResults.Ok(SentGroupInviteResponse.FromSummary(invite)),
                InviteToGroupOutcome.Forbidden => TypedResults.Forbid(),
                InviteToGroupOutcome.NotFound => TypedResults.NotFound(),
                ResendCooldownActive cooldown => cooldown.ToConflict(httpContext),
            };
        })
        .RequireRateLimiting(RateLimitingFeature.OutboundEmailPolicy)
        .ProducesErrorCode(StatusCodes.Status409Conflict, ResendCooldown.ErrorCode)
        .WithName("InviteToGroup");

        return groups;
    }
}

public sealed record InviteToGroupRequest(string Email, GroupRole Role);

public sealed record GroupInviteResponse(Guid Id, string Email, GroupRole Role, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt);

// GroupInviteResponse plus the shareable link, which exists only at send time (see GroupInviteSummary).
public sealed record SentGroupInviteResponse(Guid Id, string Email, GroupRole Role, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt, string InviteUrl)
{
    public static SentGroupInviteResponse FromSummary(GroupInviteSummary summary) =>
        new(summary.Id, summary.Email, summary.Role, summary.InvitedAt, summary.ExpiresAt, summary.InviteUrl);
}

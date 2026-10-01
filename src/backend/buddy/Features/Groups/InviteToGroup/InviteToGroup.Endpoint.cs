using System.Security.Claims;

using buddy.Common;
using buddy.Common.RateLimiting;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class InviteToGroupEndpoint
{
    public static RouteGroupBuilder MapInviteToGroup(this RouteGroupBuilder groups)
    {
        groups.MapPost("/{groupId:guid}/invites", async Task<Results<Ok<GroupInviteResponse>, NotFound, ForbidHttpResult, Conflict<ErrorEnvelope>>> (
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
                InviteToGroupOutcome.Success(var invite) => TypedResults.Ok(GroupInviteResponse.FromSummary(invite)),
                InviteToGroupOutcome.Forbidden => TypedResults.Forbid(),
                InviteToGroupOutcome.NotFound => TypedResults.NotFound(),
                ResendCooldownActive cooldown => cooldown.ToConflict(httpContext),
            };
        })
        .WithName("InviteToGroup");

        return groups;
    }
}

public sealed record InviteToGroupRequest(string Email, GroupRole Role);

public sealed record GroupInviteResponse(Guid Id, string Email, GroupRole Role, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt)
{
    public static GroupInviteResponse FromSummary(GroupInviteSummary summary) =>
        new(summary.Id, summary.Email, summary.Role, summary.InvitedAt, summary.ExpiresAt);
}

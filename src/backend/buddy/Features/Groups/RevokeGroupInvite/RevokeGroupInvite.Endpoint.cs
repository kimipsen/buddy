using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class RevokeGroupInviteEndpoint
{
    public static RouteGroupBuilder MapRevokeGroupInvite(this RouteGroupBuilder groups)
    {
        groups.MapDelete("/{groupId:guid}/invites/{inviteId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid inviteId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevokeGroupInvite.FromClaims(principal, new GroupId(groupId), inviteId);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // RevokeGroupInviteHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like RemoveGroupMember does.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RevokeGroupInvite");

        return groups;
    }
}

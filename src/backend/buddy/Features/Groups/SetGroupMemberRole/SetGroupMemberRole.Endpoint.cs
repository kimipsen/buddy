using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class SetGroupMemberRoleEndpoint
{
    public static RouteGroupBuilder MapSetGroupMemberRole(this RouteGroupBuilder groups)
    {
        groups.MapPut("/{groupId:guid}/members/{memberId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid memberId,
            SetGroupMemberRoleRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            if (request.Role == GroupRole.Owner)
            {
                return TypedResults.BadRequest("Ownership cannot be granted through this endpoint.");
            }

            var command = SetGroupMemberRole.FromClaims(principal, new GroupId(groupId), new UserId(memberId), request.Role);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("SetGroupMemberRole");

        return groups;
    }
}

public sealed record SetGroupMemberRoleRequest(GroupRole Role);

using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class RemoveGroupMemberEndpoint
{
    public static RouteGroupBuilder MapRemoveGroupMember(this RouteGroupBuilder groups)
    {
        groups.MapDelete("/{groupId:guid}/members/{memberId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid memberId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RemoveGroupMember.FromClaims(principal, new GroupId(groupId), new UserId(memberId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // RemoveGroupMemberHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RemoveGroupMember");

        return groups;
    }
}

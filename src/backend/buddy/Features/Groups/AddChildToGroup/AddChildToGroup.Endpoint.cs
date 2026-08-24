using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class AddChildToGroupEndpoint
{
    public static RouteGroupBuilder MapAddChildToGroup(this RouteGroupBuilder groups)
    {
        groups.MapPut("/{groupId:guid}/children/{childId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = AddChildToGroup.FromClaims(principal, new GroupId(groupId), new UserId(childId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // AddChildToGroupHandler never produces Validation -- no BadRequest in this
                // route's declared results, so this collapses to NotFound.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("AddChildToGroup");

        return groups;
    }
}

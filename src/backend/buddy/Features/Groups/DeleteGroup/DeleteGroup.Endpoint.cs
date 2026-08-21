using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Groups;

public static class DeleteGroupEndpoint
{
    public static RouteGroupBuilder MapDeleteGroup(this RouteGroupBuilder groups)
    {
        groups.MapDelete("/{groupId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid groupId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = DeleteGroup.FromClaims(principal, new GroupId(groupId));
            var access = await bus.InvokeAsync<GroupAccess>(command, cancellationToken);

            return access switch
            {
                GroupAccess.Allowed => TypedResults.NoContent(),
                GroupAccess.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("DeleteGroup");

        return groups;
    }
}

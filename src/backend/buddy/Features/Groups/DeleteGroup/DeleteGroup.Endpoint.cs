using System.Security.Claims;

using buddy.Common;

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
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // DeleteGroupHandler never produces Validation -- there's no BadRequest in this
                // route's declared results, so this collapses to NotFound like the others.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("DeleteGroup");

        return groups;
    }
}

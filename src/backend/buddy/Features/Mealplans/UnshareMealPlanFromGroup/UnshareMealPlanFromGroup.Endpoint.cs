using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class UnshareMealPlanFromGroupEndpoint
{
    public static RouteGroupBuilder MapUnshareMealPlanFromGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapDelete("/children/{childId:guid}/plan/groups/{groupId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid groupId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = UnshareMealPlanFromGroup.FromClaims(principal, new UserId(childId), new GroupId(groupId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // UnshareMealPlanFromGroupHandler never produces Validation -- there's no
                // BadRequest in this route's declared results, so this collapses to NotFound.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("UnshareMealPlanFromGroup");

        return mealplans;
    }
}

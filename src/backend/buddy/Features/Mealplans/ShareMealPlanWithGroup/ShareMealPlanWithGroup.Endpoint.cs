using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ShareMealPlanWithGroupEndpoint
{
    public static RouteGroupBuilder MapShareMealPlanWithGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPut("/children/{childId:guid}/plan/groups/{groupId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid groupId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ShareMealPlanWithGroup.FromClaims(principal, new UserId(childId), new GroupId(groupId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // ShareMealPlanWithGroupHandler never produces Validation -- there's no
                // BadRequest in this route's declared results, so this collapses to NotFound.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ShareMealPlanWithGroup");

        return mealplans;
    }
}

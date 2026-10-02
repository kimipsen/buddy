using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class GetSharedGroupEndpoint
{
    public static RouteGroupBuilder MapGetSharedGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/children/{childId:guid}/plan/groups", async Task<Results<Ok<SharedGroupResponse>, NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = GetSharedGroup.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<MealplanGroupShare>>(query, cancellationToken);

            return result switch
            {
                Result<MealplanGroupShare>.Success(MealplanGroupShare.Shared(var groupId, var groupName)) => TypedResults.Ok(new SharedGroupResponse(groupId.Value, groupName)),
                Result<MealplanGroupShare>.Success(MealplanGroupShare.NotShared) => TypedResults.NoContent(),
                Result<MealplanGroupShare>.Forbidden => TypedResults.Forbid(),
                Result<MealplanGroupShare>.NotFound => TypedResults.NotFound(),
                // GetSharedGroupHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others.
                Result<MealplanGroupShare>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetSharedGroup");

        return mealplans;
    }
}

// 204 No Content when the plan isn't shared with a group.
public sealed record SharedGroupResponse(Guid GroupId, string GroupName);

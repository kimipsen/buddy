using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class GetGroupMealplanStatusEndpoint
{
    public static RouteGroupBuilder MapGetGroupMealplanStatus(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/groups/{groupId:guid}/status", async Task<Results<Ok<GroupMealplanStatusResponse>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid groupId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = GetGroupMealplanStatus.FromClaims(principal, new GroupId(groupId));
            var result = await bus.InvokeAsync<Result<GroupMealplanStatus>>(query, cancellationToken);

            return result switch
            {
                Result<GroupMealplanStatus>.Success(var status) => TypedResults.Ok(new GroupMealplanStatusResponse(status.HasSharedPlan)),
                Result<GroupMealplanStatus>.Forbidden => TypedResults.Forbid(),
                Result<GroupMealplanStatus>.NotFound => TypedResults.NotFound(),
                // GetGroupMealplanStatusHandler never produces Validation -- there's no
                // BadRequest in this route's declared results, so this collapses to NotFound
                // like the sibling group-keyed endpoints.
                Result<GroupMealplanStatus>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetGroupMealplanStatus");

        return mealplans;
    }
}

public sealed record GroupMealplanStatusResponse(bool HasSharedPlan);

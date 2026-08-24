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
        mealplans.MapGet("/children/{childId:guid}/plan/groups", async Task<Results<Ok<SharedGroupResponse>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = GetSharedGroup.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<SharedMealplanGroup?>>(query, cancellationToken);

            return result switch
            {
                Result<SharedMealplanGroup?>.Success(var group) => TypedResults.Ok(new SharedGroupResponse(group?.Id.Value, group?.Name)),
                Result<SharedMealplanGroup?>.Forbidden => TypedResults.Forbid(),
                Result<SharedMealplanGroup?>.NotFound => TypedResults.NotFound(),
                // GetSharedGroupHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others.
                Result<SharedMealplanGroup?>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetSharedGroup");

        return mealplans;
    }
}

public sealed record SharedGroupResponse(Guid? GroupId, string? GroupName);

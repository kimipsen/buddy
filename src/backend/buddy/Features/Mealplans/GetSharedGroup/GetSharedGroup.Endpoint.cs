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
            var result = await bus.InvokeAsync<Result<GroupId?>>(query, cancellationToken);

            return result switch
            {
                Result<GroupId?>.Success(var groupId) => TypedResults.Ok(new SharedGroupResponse(groupId?.Value)),
                Result<GroupId?>.Forbidden => TypedResults.Forbid(),
                Result<GroupId?>.NotFound => TypedResults.NotFound(),
                // GetSharedGroupHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others.
                Result<GroupId?>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetSharedGroup");

        return mealplans;
    }
}

public sealed record SharedGroupResponse(Guid? GroupId);

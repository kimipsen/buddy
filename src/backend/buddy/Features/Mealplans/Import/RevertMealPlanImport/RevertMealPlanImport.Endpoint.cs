using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class RevertMealPlanImportEndpoint
{
    public static RouteGroupBuilder MapRevertMealPlanImport(this RouteGroupBuilder mealplans)
    {
        mealplans.MapDelete("/children/{childId:guid}/imports/{importId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid importId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevertMealPlanImport.FromClaims(principal, new UserId(childId), new MealPlanImportId(importId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // No validator: nothing produces Validation here.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RevertMealPlanImport");

        return mealplans;
    }

    public static RouteGroupBuilder MapRevertMealPlanImportForGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapDelete("/groups/{groupId:guid}/imports/{importId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid importId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevertMealPlanImportForGroup.FromClaims(principal, new GroupId(groupId), new MealPlanImportId(importId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // Reachable for a caller whose group policy grants View but not Manage.
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                // No validator: nothing produces Validation here.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RevertMealPlanImportForGroup");

        return mealplans;
    }
}

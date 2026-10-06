using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ListMealPlanImportsEndpoint
{
    public static RouteGroupBuilder MapListMealPlanImports(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/children/{childId:guid}/imports", async Task<Results<Ok<IReadOnlyList<MealPlanImportSummary>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListMealPlanImports.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<IReadOnlyList<MealPlanImportSummary>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyList<MealPlanImportSummary>>.Success(var imports) => TypedResults.Ok(imports),
                Result<IReadOnlyList<MealPlanImportSummary>>.Forbidden => TypedResults.Forbid(),
                Result<IReadOnlyList<MealPlanImportSummary>>.NotFound => TypedResults.NotFound(),
                // No validator: nothing produces Validation here.
                Result<IReadOnlyList<MealPlanImportSummary>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListMealPlanImports");

        return mealplans;
    }

    public static RouteGroupBuilder MapListMealPlanImportsForGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/groups/{groupId:guid}/imports", async Task<Results<Ok<IReadOnlyList<MealPlanImportSummary>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid groupId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListMealPlanImportsForGroup.FromClaims(principal, new GroupId(groupId));
            var result = await bus.InvokeAsync<Result<IReadOnlyList<MealPlanImportSummary>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyList<MealPlanImportSummary>>.Success(var imports) => TypedResults.Ok(imports),
                Result<IReadOnlyList<MealPlanImportSummary>>.NotFound => TypedResults.NotFound(),
                // Reachable for a caller whose group policy grants View but not Manage.
                Result<IReadOnlyList<MealPlanImportSummary>>.Forbidden => TypedResults.Forbid(),
                // No validator: nothing produces Validation here.
                Result<IReadOnlyList<MealPlanImportSummary>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListMealPlanImportsForGroup");

        return mealplans;
    }
}

using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ListMealsEndpoint
{
    public static RouteGroupBuilder MapListMeals(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/children/{childId:guid}/meals", async Task<Results<Ok<IReadOnlyCollection<MealResponse>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<Meal>>>(ListMeals.FromClaims(principal, new UserId(childId)), cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<Meal>>.Success(var meals) =>
                    TypedResults.Ok<IReadOnlyCollection<MealResponse>>([.. meals.Select(MealResponse.FromMeal)]),
                Result<IReadOnlyCollection<Meal>>.Forbidden => TypedResults.Forbid(),
                Result<IReadOnlyCollection<Meal>>.NotFound => TypedResults.NotFound(),
                // ListMealsHandler never produces Validation -- there's no BadRequest in this
                // route's declared results, so this collapses to NotFound like the others.
                Result<IReadOnlyCollection<Meal>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListMeals");

        return mealplans;
    }
}

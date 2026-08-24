using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ListMealsForGroupEndpoint
{
    public static RouteGroupBuilder MapListMealsForGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/groups/{groupId:guid}/meals", async Task<Results<Ok<IReadOnlyCollection<MealResponse>>, NotFound>> (
            ClaimsPrincipal principal,
            Guid groupId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<Meal>>>(ListMealsForGroup.FromClaims(principal, new GroupId(groupId)), cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<Meal>>.Success(var meals) =>
                    TypedResults.Ok<IReadOnlyCollection<MealResponse>>([.. meals.Select(MealResponse.FromMeal)]),
                Result<IReadOnlyCollection<Meal>>.NotFound => TypedResults.NotFound(),
                // MealplanGroupAuthorization never produces Forbidden/Validation -- there's no
                // ForbidHttpResult/BadRequest in this route's declared results, so both collapse
                // to NotFound like ListMeals's own child-keyed route does.
                Result<IReadOnlyCollection<Meal>>.Forbidden => TypedResults.NotFound(),
                Result<IReadOnlyCollection<Meal>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListMealsForGroup");

        return mealplans;
    }
}

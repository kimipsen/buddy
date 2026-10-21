using System.Security.Claims;

using buddy.Common;
using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class LinkMealToRecipeEndpoint
{
    public static RouteGroupBuilder MapLinkMealToRecipe(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPut("/children/{childId:guid}/meals/{mealId:guid}/recipe", async (
            ClaimsPrincipal principal,
            Guid childId,
            Guid mealId,
            LinkMealToRecipeRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = LinkMealToRecipe.FromClaims(principal, new UserId(childId), new MealId(mealId), request.RecipeId);
            var outcome = await bus.InvokeAsync<MealieOutcome<Meal>>(command, cancellationToken);

            return outcome.Map(MealResponse.FromMeal).ToHttpResult(httpContext);
        })
        .RequireRateLimiting(RateLimitingFeature.MealiePolicy)
        .ProducesMealieErrors()
        .WithName("LinkMealToRecipe");

        return mealplans;
    }

    public static RouteGroupBuilder MapUnlinkMealRecipe(this RouteGroupBuilder mealplans)
    {
        mealplans.MapDelete("/children/{childId:guid}/meals/{mealId:guid}/recipe", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid mealId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<Unit>>(UnlinkMealRecipe.FromClaims(principal, new UserId(childId), new MealId(mealId)), cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // Never produced: the handler has no validator.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("UnlinkMealRecipe");

        return mealplans;
    }
}

public sealed record LinkMealToRecipeRequest(Guid RecipeId);

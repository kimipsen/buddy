using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ListMealPlanIcalTokensEndpoint
{
    public static RouteGroupBuilder MapListMealPlanIcalTokens(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/children/{childId:guid}/ical-tokens", async Task<Results<Ok<IReadOnlyCollection<MealPlanIcalTokenSummary>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListMealPlanIcalTokens.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<MealPlanIcalTokenSummary>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<MealPlanIcalTokenSummary>>.Success(var tokens) => TypedResults.Ok(tokens),
                Result<IReadOnlyCollection<MealPlanIcalTokenSummary>>.Forbidden => TypedResults.Forbid(),
                Result<IReadOnlyCollection<MealPlanIcalTokenSummary>>.NotFound => TypedResults.NotFound(),
                // ListMealPlanIcalTokensHandler never produces Validation -- there's no BadRequest
                // in this route's declared results, so this collapses to NotFound like the others.
                Result<IReadOnlyCollection<MealPlanIcalTokenSummary>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListMealPlanIcalTokens");

        return mealplans;
    }
}

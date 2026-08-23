using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ListMealPlanEndpoint
{
    public static RouteGroupBuilder MapListMealPlan(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/children/{childId:guid}/plan", async Task<Results<Ok<IReadOnlyCollection<MealPlanEntry>>, NotFound, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListMealPlan.FromClaims(principal, new UserId(childId), from, to);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<MealPlanEntry>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<MealPlanEntry>>.Success(var entries) => TypedResults.Ok(entries),
                Result<IReadOnlyCollection<MealPlanEntry>>.Validation(var message) => TypedResults.BadRequest(message),
                Result<IReadOnlyCollection<MealPlanEntry>>.NotFound => TypedResults.NotFound(),
                // CheckView never returns Forbidden, so this is unreachable today -- there's no
                // ForbidHttpResult in this route's declared results, so it collapses to NotFound.
                Result<IReadOnlyCollection<MealPlanEntry>>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("ListMealPlan");

        return mealplans;
    }
}

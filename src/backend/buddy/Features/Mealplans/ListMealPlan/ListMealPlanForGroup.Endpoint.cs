using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ListMealPlanForGroupEndpoint
{
    public static RouteGroupBuilder MapListMealPlanForGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/groups/{groupId:guid}/plan", async Task<Results<Ok<IReadOnlyCollection<MealPlanEntry>>, NotFound, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListMealPlanForGroup.FromClaims(principal, new GroupId(groupId), from, to);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<MealPlanEntry>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<MealPlanEntry>>.Success(var entries) => TypedResults.Ok(entries),
                Result<IReadOnlyCollection<MealPlanEntry>>.Validation(var message) => TypedResults.BadRequest(message),
                Result<IReadOnlyCollection<MealPlanEntry>>.NotFound => TypedResults.NotFound(),
                // MealplanGroupAuthorization never produces Forbidden -- there's no
                // ForbidHttpResult in this route's declared results, so it collapses to NotFound.
                Result<IReadOnlyCollection<MealPlanEntry>>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("ListMealPlanForGroup");

        return mealplans;
    }
}

using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class AssignMealToSlotForGroupEndpoint
{
    public static RouteGroupBuilder MapAssignMealToSlotForGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPut("/groups/{groupId:guid}/plan", async Task<Results<Ok<MealPlanEntry>, NotFound, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid groupId,
            DateOnly date,
            MealSlot slot,
            AssignMealToSlotRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = AssignMealToSlotForGroup.FromClaims(principal, new GroupId(groupId), date, slot, new MealId(request.MealId), request.Notes);
            var result = await bus.InvokeAsync<Result<MealPlanEntry>>(command, cancellationToken);

            return result switch
            {
                Result<MealPlanEntry>.Success(var entry) => TypedResults.Ok(entry),
                Result<MealPlanEntry>.Validation(var message) => TypedResults.BadRequest(message),
                Result<MealPlanEntry>.NotFound => TypedResults.NotFound(),
                // MealplanGroupAuthorization never produces Forbidden -- there's no
                // ForbidHttpResult in this route's declared results, so it collapses to NotFound.
                Result<MealPlanEntry>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("AssignMealToSlotForGroup");

        return mealplans;
    }
}

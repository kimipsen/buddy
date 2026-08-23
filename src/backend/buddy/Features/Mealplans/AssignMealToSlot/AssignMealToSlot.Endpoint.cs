using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class AssignMealToSlotEndpoint
{
    public static RouteGroupBuilder MapAssignMealToSlot(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPut("/children/{childId:guid}/plan", async Task<Results<Ok<MealPlanEntry>, NotFound, ForbidHttpResult, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly date,
            MealSlot slot,
            AssignMealToSlotRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = AssignMealToSlot.FromClaims(principal, new UserId(childId), date, slot, new MealId(request.MealId), request.Notes);
            var result = await bus.InvokeAsync<Result<MealPlanEntry>>(command, cancellationToken);

            return result switch
            {
                Result<MealPlanEntry>.Success(var entry) => TypedResults.Ok(entry),
                Result<MealPlanEntry>.Forbidden => TypedResults.Forbid(),
                Result<MealPlanEntry>.Validation(var message) => TypedResults.BadRequest(message),
                Result<MealPlanEntry>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("AssignMealToSlot");

        return mealplans;
    }
}

public sealed record AssignMealToSlotRequest(Guid MealId, string? Notes);

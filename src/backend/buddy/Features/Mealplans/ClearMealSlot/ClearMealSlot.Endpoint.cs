using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ClearMealSlotEndpoint
{
    public static RouteGroupBuilder MapClearMealSlot(this RouteGroupBuilder mealplans)
    {
        mealplans.MapDelete("/children/{childId:guid}/plan", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly date,
            MealSlot slot,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ClearMealSlot.FromClaims(principal, new UserId(childId), date, slot);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // ClearMealSlotHandler never produces Validation -- there's no BadRequest in this
                // route's declared results, so this collapses to NotFound like the others.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ClearMealSlot");

        return mealplans;
    }
}

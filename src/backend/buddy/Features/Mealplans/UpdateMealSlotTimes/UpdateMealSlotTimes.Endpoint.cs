using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class UpdateMealSlotTimesEndpoint
{
    public static RouteGroupBuilder MapUpdateMealSlotTimes(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPut("/children/{childId:guid}/slot-times", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            UpdateMealSlotTimesRequest request,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateMealSlotTimes.FromClaims(principal, new UserId(childId), request.Times.ToImmutableDictionary());
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // UpdateMealSlotTimesHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateMealSlotTimes");

        return mealplans;
    }
}

// A partial map -- only the slots being changed need to be included; any slot left out keeps its
// current value (or MealSlotDefaultTimes, if never configured).
public sealed record UpdateMealSlotTimesRequest(Dictionary<MealSlot, TimeOnly> Times);

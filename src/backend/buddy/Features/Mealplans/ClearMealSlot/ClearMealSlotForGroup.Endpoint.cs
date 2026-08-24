using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ClearMealSlotForGroupEndpoint
{
    public static RouteGroupBuilder MapClearMealSlotForGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapDelete("/groups/{groupId:guid}/plan", async Task<Results<NoContent, NotFound>> (
            ClaimsPrincipal principal,
            Guid groupId,
            DateOnly date,
            MealSlot slot,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ClearMealSlotForGroup.FromClaims(principal, new GroupId(groupId), date, slot);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // MealplanGroupAuthorization never produces Forbidden/Validation -- there's no
                // ForbidHttpResult/BadRequest in this route's declared results, so both collapse
                // to NotFound like ClearMealSlot's own child-keyed route does.
                Result<Unit>.Forbidden => TypedResults.NotFound(),
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ClearMealSlotForGroup");

        return mealplans;
    }
}

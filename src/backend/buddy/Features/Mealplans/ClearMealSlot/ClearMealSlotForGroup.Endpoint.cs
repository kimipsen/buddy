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
        mealplans.MapDelete("/groups/{groupId:guid}/plan", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
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
                // Reachable for a caller whose group policy grants View but not Manage.
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                // MealplanGroupAuthorization never produces Validation -- there's no BadRequest
                // in this route's declared results, so it collapses to NotFound like
                // ClearMealSlot's own child-keyed route does.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ClearMealSlotForGroup");

        return mealplans;
    }
}

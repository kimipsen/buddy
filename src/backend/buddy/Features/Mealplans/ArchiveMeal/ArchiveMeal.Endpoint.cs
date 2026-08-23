using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ArchiveMealEndpoint
{
    public static RouteGroupBuilder MapArchiveMeal(this RouteGroupBuilder mealplans)
    {
        mealplans.MapDelete("/children/{childId:guid}/meals/{mealId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid mealId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ArchiveMeal.FromClaims(principal, new UserId(childId), new MealId(mealId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // ArchiveMealHandler never produces Validation -- there's no BadRequest in this
                // route's declared results, so this collapses to NotFound like the others.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ArchiveMeal");

        return mealplans;
    }
}

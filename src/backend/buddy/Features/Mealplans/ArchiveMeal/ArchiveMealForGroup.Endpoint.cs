using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ArchiveMealForGroupEndpoint
{
    public static RouteGroupBuilder MapArchiveMealForGroup(this RouteGroupBuilder mealplans)
    {
        mealplans.MapDelete("/groups/{groupId:guid}/meals/{mealId:guid}", async Task<Results<NoContent, NotFound>> (
            ClaimsPrincipal principal,
            Guid groupId,
            Guid mealId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ArchiveMealForGroup.FromClaims(principal, new GroupId(groupId), new MealId(mealId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // MealplanGroupAuthorization never produces Forbidden/Validation -- there's no
                // ForbidHttpResult/BadRequest in this route's declared results, so both collapse
                // to NotFound like ArchiveMeal's own child-keyed route does.
                Result<Unit>.Forbidden => TypedResults.NotFound(),
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ArchiveMealForGroup");

        return mealplans;
    }
}

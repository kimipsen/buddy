using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class RevokeMealPlanIcalTokenEndpoint
{
    public static RouteGroupBuilder MapRevokeMealPlanIcalToken(this RouteGroupBuilder mealplans)
    {
        mealplans.MapDelete("/children/{childId:guid}/ical-tokens/{tokenId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid tokenId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevokeMealPlanIcalToken.FromClaims(principal, new UserId(childId), new IcalTokenId(tokenId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // RevokeMealPlanIcalTokenHandler never produces Validation -- there's no BadRequest
                // in this route's declared results, so this collapses to NotFound like the others.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RevokeMealPlanIcalToken");

        return mealplans;
    }
}

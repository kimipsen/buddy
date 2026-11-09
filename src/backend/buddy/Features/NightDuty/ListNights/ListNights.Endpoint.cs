using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.NightDuty;

public static class ListNightsEndpoint
{
    public static RouteGroupBuilder MapListNights(this RouteGroupBuilder nightDuty)
    {
        nightDuty.MapGet("/children/{childId:guid}/nights", async Task<Results<Ok<IReadOnlyCollection<Night>>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = ListNights.FromClaims(principal, new UserId(childId), from, to);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<Night>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<Night>>.Success(var nights) => TypedResults.Ok(nights),
                Result<IReadOnlyCollection<Night>>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<IReadOnlyCollection<Night>>.NotFound => TypedResults.NotFound(),
                // CheckView never returns Forbidden -- there's no ForbidHttpResult in this route's
                // declared results, so it collapses to NotFound.
                Result<IReadOnlyCollection<Night>>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("ListNights");

        return nightDuty;
    }
}

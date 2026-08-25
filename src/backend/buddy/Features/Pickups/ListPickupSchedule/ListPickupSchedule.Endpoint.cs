using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Pickups;

public static class ListPickupScheduleEndpoint
{
    public static RouteGroupBuilder MapListPickupSchedule(this RouteGroupBuilder pickups)
    {
        pickups.MapGet("/children/{childId:guid}/schedule", async Task<Results<Ok<IReadOnlyCollection<PickupOccurrence>>, NotFound, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListPickupSchedule.FromClaims(principal, new UserId(childId), from, to);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<PickupOccurrence>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<PickupOccurrence>>.Success(var occurrences) => TypedResults.Ok(occurrences),
                Result<IReadOnlyCollection<PickupOccurrence>>.Validation(var message) => TypedResults.BadRequest(message),
                Result<IReadOnlyCollection<PickupOccurrence>>.NotFound => TypedResults.NotFound(),
                // CheckView never returns Forbidden, so this is unreachable today -- there's no
                // ForbidHttpResult in this route's declared results, so it collapses to NotFound.
                Result<IReadOnlyCollection<PickupOccurrence>>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("ListPickupSchedule");

        return pickups;
    }
}

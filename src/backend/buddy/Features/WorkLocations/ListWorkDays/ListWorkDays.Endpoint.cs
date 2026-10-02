using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.WorkLocations;

public static class ListWorkDaysEndpoint
{
    public static RouteGroupBuilder MapListWorkDays(this RouteGroupBuilder workLocations)
    {
        workLocations.MapGet("/guardians/{guardianId:guid}/days", async Task<Results<Ok<IReadOnlyCollection<WorkDay>>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid guardianId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = ListWorkDays.FromClaims(principal, new UserId(guardianId), from, to);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<WorkDay>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<WorkDay>>.Success(var days) => TypedResults.Ok(days),
                Result<IReadOnlyCollection<WorkDay>>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<IReadOnlyCollection<WorkDay>>.NotFound => TypedResults.NotFound(),
                // CheckView never returns Forbidden -- there's no ForbidHttpResult in this route's
                // declared results, so it collapses to NotFound.
                Result<IReadOnlyCollection<WorkDay>>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("ListWorkDays");

        return workLocations;
    }
}

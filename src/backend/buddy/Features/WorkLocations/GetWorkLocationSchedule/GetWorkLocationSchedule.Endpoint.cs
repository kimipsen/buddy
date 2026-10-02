using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.WorkLocations;

public static class GetWorkLocationScheduleEndpoint
{
    public static RouteGroupBuilder MapGetWorkLocationSchedule(this RouteGroupBuilder workLocations)
    {
        workLocations.MapGet("/guardians/{guardianId:guid}", async Task<Results<Ok<WorkLocationScheduleResponse>, NotFound>> (
            ClaimsPrincipal principal,
            Guid guardianId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = GetWorkLocationSchedule.FromClaims(principal, new UserId(guardianId));
            var result = await bus.InvokeAsync<Result<WorkLocationScheduleResponse>>(query, cancellationToken);

            return result switch
            {
                Result<WorkLocationScheduleResponse>.Success(var schedule) => TypedResults.Ok(schedule),
                Result<WorkLocationScheduleResponse>.NotFound => TypedResults.NotFound(),
                // CheckView never returns Forbidden and there's no input to validate -- neither is
                // in this route's declared results, so both collapse to NotFound.
                Result<WorkLocationScheduleResponse>.Forbidden => TypedResults.NotFound(),
                Result<WorkLocationScheduleResponse>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetWorkLocationSchedule");

        return workLocations;
    }
}

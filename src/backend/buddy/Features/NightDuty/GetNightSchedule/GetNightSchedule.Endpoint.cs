using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.NightDuty;

public static class GetNightScheduleEndpoint
{
    public static RouteGroupBuilder MapGetNightSchedule(this RouteGroupBuilder nightDuty)
    {
        nightDuty.MapGet("/children/{childId:guid}", async Task<Results<Ok<NightScheduleResponse>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = GetNightSchedule.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<NightScheduleResponse>>(query, cancellationToken);

            return result switch
            {
                Result<NightScheduleResponse>.Success(var response) => TypedResults.Ok(response),
                Result<NightScheduleResponse>.Forbidden => TypedResults.Forbid(),
                Result<NightScheduleResponse>.NotFound => TypedResults.NotFound(),
                // The handler has no validator -- a route-bound guid is the only input.
                Result<NightScheduleResponse>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetNightSchedule");

        return nightDuty;
    }
}

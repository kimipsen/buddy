using System.Security.Claims;

using buddy.Common;
using buddy.Features.Calendars;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Users;

public static class UpdateCurrentTimeZoneEndpoint
{
    public static RouteGroupBuilder MapUpdateCurrentTimeZone(this RouteGroupBuilder users)
    {
        users.MapPatch("/me/timezone", async Task<Results<Ok<UserResponse>, BadRequest<ErrorEnvelope>, NotFound>> (
            ClaimsPrincipal principal,
            UpdateTimeZoneRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateTimeZone.FromClaims(principal, new TimeZoneId(request.TimeZoneId));

            var result = await bus.InvokeAsync<Result<User>>(command, cancellationToken);

            return result switch
            {
                Result<User>.Success(var user) => TypedResults.Ok(UserResponse.FromUser(user)),
                Result<User>.NotFound => TypedResults.NotFound(),
                Result<User>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                // UpdateTimeZoneHandler never produces Forbidden -- collapsed to NotFound since
                // this route declares no other status for it.
                Result<User>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateCurrentTimeZone");

        return users;
    }
}

public sealed record UpdateTimeZoneRequest(string TimeZoneId);

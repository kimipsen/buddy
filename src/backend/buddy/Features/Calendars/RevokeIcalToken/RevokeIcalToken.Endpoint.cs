using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class RevokeIcalTokenEndpoint
{
    public static RouteGroupBuilder MapRevokeIcalToken(this RouteGroupBuilder calendars)
    {
        calendars.MapDelete("/{calendarId:guid}/ical-tokens/{tokenId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid tokenId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevokeIcalToken.FromClaims(principal, new CalendarId(calendarId), new IcalTokenId(tokenId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // RevokeIcalTokenHandler never produces Validation -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound like the others.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("RevokeCalendarIcalToken");

        return calendars;
    }
}

using System.Security.Claims;

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
            var access = await bus.InvokeAsync<CalendarAccess>(command, cancellationToken);

            return access switch
            {
                CalendarAccess.Allowed => TypedResults.NoContent(),
                CalendarAccess.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("RevokeCalendarIcalToken");

        return calendars;
    }
}

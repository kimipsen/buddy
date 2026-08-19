using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class DeleteCalendarEndpoint
{
    public static RouteGroupBuilder MapDeleteCalendar(this RouteGroupBuilder calendars)
    {
        calendars.MapDelete("/{calendarId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var access = await bus.InvokeAsync<CalendarAccess>(DeleteCalendar.FromClaims(principal, new CalendarId(calendarId)), cancellationToken);

            return access switch
            {
                CalendarAccess.Allowed => TypedResults.NoContent(),
                CalendarAccess.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("DeleteCalendar");

        return calendars;
    }
}

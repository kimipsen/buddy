using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class DeleteItemEndpoint
{
    public static RouteGroupBuilder MapDeleteItem(this RouteGroupBuilder calendars)
    {
        calendars.MapDelete("/{calendarId:guid}/items/{itemId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            Guid itemId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = DeleteItem.FromClaims(principal, new CalendarId(calendarId), new CalendarItemId(itemId));
            var access = await bus.InvokeAsync<CalendarAccess>(command, cancellationToken);

            return access switch
            {
                CalendarAccess.Allowed => TypedResults.NoContent(),
                CalendarAccess.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("DeleteCalendarItem");

        return calendars;
    }
}

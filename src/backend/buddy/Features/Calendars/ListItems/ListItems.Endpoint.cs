using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class ListItemsEndpoint
{
    public static RouteGroupBuilder MapListItems(this RouteGroupBuilder calendars)
    {
        calendars.MapGet("/{calendarId:guid}/items", async Task<Results<Ok<IReadOnlyCollection<CalendarItemResponse>>, NotFound>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<ListItemsResult>(ListItems.FromClaims(principal, new CalendarId(calendarId)), cancellationToken);

            if (result.Access != CalendarAccess.Allowed)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok<IReadOnlyCollection<CalendarItemResponse>>([.. result.Items.Select(CalendarItemResponse.FromItem)]);
        })
        .WithName("ListCalendarItems");

        return calendars;
    }
}

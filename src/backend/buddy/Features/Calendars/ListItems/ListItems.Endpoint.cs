using System.Security.Claims;

using buddy.Common;

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
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<CalendarItem>>>(ListItems.FromClaims(principal, new CalendarId(calendarId)), cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<CalendarItem>>.Success(var items) =>
                    TypedResults.Ok<IReadOnlyCollection<CalendarItemResponse>>([.. items.Select(CalendarItemResponse.FromItem)]),
                Result<IReadOnlyCollection<CalendarItem>>.NotFound => TypedResults.NotFound(),
                // CheckView never returns Forbidden or Validation, so these are unreachable today
                // -- collapsed to NotFound since this route declares no other status for them.
                Result<IReadOnlyCollection<CalendarItem>>.Forbidden => TypedResults.NotFound(),
                Result<IReadOnlyCollection<CalendarItem>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListCalendarItems");

        return calendars;
    }
}

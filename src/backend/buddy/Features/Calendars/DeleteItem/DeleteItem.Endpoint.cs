using System.Security.Claims;

using buddy.Common;

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
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("DeleteCalendarItem");

        return calendars;
    }
}

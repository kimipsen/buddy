using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class ListOccurrencesEndpoint
{
    public static RouteGroupBuilder MapListOccurrences(this RouteGroupBuilder calendars)
    {
        calendars.MapGet("/{calendarId:guid}/occurrences", async Task<Results<Ok<IReadOnlyCollection<CalendarItemOccurrence>>, NotFound, BadRequest<string>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListOccurrences.FromClaims(principal, new CalendarId(calendarId), from, to);
            var result = await bus.InvokeAsync<ListOccurrencesResult>(query, cancellationToken);

            if (result.ValidationError is not null)
            {
                return TypedResults.BadRequest(result.ValidationError);
            }

            if (result.Access != CalendarAccess.Allowed)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(result.Occurrences);
        })
        .WithName("ListCalendarOccurrences");

        return calendars;
    }
}

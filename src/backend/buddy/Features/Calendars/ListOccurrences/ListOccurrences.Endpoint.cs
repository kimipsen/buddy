using System.Security.Claims;

using buddy.Common;

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
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<CalendarItemOccurrence>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<CalendarItemOccurrence>>.Success(var occurrences) => TypedResults.Ok(occurrences),
                Result<IReadOnlyCollection<CalendarItemOccurrence>>.Validation(var message) => TypedResults.BadRequest(message),
                _ => TypedResults.NotFound(),
            };
        })
        .WithName("ListCalendarOccurrences");

        return calendars;
    }
}

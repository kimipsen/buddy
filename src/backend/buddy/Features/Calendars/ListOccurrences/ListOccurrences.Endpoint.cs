using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class ListOccurrencesEndpoint
{
    public static RouteGroupBuilder MapListOccurrences(this RouteGroupBuilder calendars)
    {
        calendars.MapGet("/{calendarId:guid}/occurrences", async Task<Results<Ok<IReadOnlyCollection<CalendarItemOccurrence>>, NotFound, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = ListOccurrences.FromClaims(principal, new CalendarId(calendarId), from, to);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<CalendarItemOccurrence>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<CalendarItemOccurrence>>.Success(var occurrences) => TypedResults.Ok(occurrences),
                Result<IReadOnlyCollection<CalendarItemOccurrence>>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<IReadOnlyCollection<CalendarItemOccurrence>>.NotFound => TypedResults.NotFound(),
                // CheckView never returns Forbidden, so this is unreachable today -- there's no
                // ForbidHttpResult in this route's declared results, so it collapses to NotFound.
                Result<IReadOnlyCollection<CalendarItemOccurrence>>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("ListCalendarOccurrences");

        return calendars;
    }
}

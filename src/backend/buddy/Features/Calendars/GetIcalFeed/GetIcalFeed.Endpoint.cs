using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class GetIcalFeedEndpoint
{
    public static RouteGroupBuilder MapGetIcalFeed(this RouteGroupBuilder calendars)
    {
        calendars.MapGet("/{calendarId:guid}/ical/{token}", async Task<Results<ContentHttpResult, NotFound>> (
            Guid calendarId,
            string token,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = new GetIcalFeed(new CalendarId(calendarId), token);
            var result = await bus.InvokeAsync<Result<string>>(query, cancellationToken);

            if (result is not Result<string>.Success(var icsContent))
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Text(icsContent, "text/calendar");
        })
        .AllowAnonymous()
        .WithName("GetCalendarIcalFeed");

        return calendars;
    }
}

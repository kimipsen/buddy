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
            var result = await bus.InvokeAsync<GetIcalFeedResult>(query, cancellationToken);

            if (result.IcsContent is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Text(result.IcsContent, "text/calendar");
        })
        .AllowAnonymous()
        .WithName("GetCalendarIcalFeed");

        return calendars;
    }
}

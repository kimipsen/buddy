using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Calendars;

public static class GetCalendarEndpoint
{
    public static RouteGroupBuilder MapGetCalendar(this RouteGroupBuilder calendars)
    {
        calendars.MapGet("/{calendarId:guid}", async Task<Results<Ok<CalendarResponse>, NotFound>> (
            ClaimsPrincipal principal,
            Guid calendarId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<Calendar>>(GetCalendar.FromClaims(principal, new CalendarId(calendarId)), cancellationToken);

            if (result is not Result<Calendar>.Success(var calendar))
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(CalendarResponse.FromCalendar(calendar));
        })
        .WithName("GetCalendar");

        return calendars;
    }
}

public sealed record CalendarMemberResponse(Guid UserId, CalendarRole Role);

public sealed record CalendarResponse(CalendarId Id, string Name, string TimeZoneId, IReadOnlyCollection<CalendarMemberResponse> Members)
{
    public static CalendarResponse FromCalendar(Calendar calendar) => new(
        calendar.Id,
        calendar.Name,
        calendar.TimeZoneId.Value,
        [.. calendar.Members.Select(m => new CalendarMemberResponse(m.Key.Value, m.Value))]);
}

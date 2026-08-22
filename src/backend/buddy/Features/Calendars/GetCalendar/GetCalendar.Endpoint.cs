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

            return result switch
            {
                Result<Calendar>.Success(var calendar) => TypedResults.Ok(CalendarResponse.FromCalendar(calendar)),
                Result<Calendar>.NotFound => TypedResults.NotFound(),
                // CheckView never returns Forbidden or Validation, so these are unreachable today
                // -- collapsed to NotFound since this route declares no other status for them.
                Result<Calendar>.Forbidden => TypedResults.NotFound(),
                Result<Calendar>.Validation => TypedResults.NotFound(),
            };
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

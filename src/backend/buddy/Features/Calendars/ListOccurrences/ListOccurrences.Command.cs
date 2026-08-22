using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record ListOccurrences(UserId? UserId, CalendarId CalendarId, DateOnly From, DateOnly To)
{
    public static ListOccurrences FromClaims(ClaimsPrincipal principal, CalendarId calendarId, DateOnly from, DateOnly to) =>
        new(principal.GetUserId(), calendarId, from, to);
}

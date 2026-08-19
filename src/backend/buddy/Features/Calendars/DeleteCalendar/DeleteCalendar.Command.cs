using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record DeleteCalendar(UserId? UserId, CalendarId CalendarId)
{
    public static DeleteCalendar FromClaims(ClaimsPrincipal principal, CalendarId calendarId) => new(principal.GetUserId(), calendarId);
}

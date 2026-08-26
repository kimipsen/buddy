using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record UpdateCalendarIcon(UserId? UserId, CalendarId CalendarId, Icon Icon)
{
    public static UpdateCalendarIcon FromClaims(ClaimsPrincipal principal, CalendarId calendarId, Icon icon) =>
        new(principal.GetUserId(), calendarId, icon);
}

using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record GetCalendar(KeycloakSubject Subject, CalendarId CalendarId)
{
    public static GetCalendar FromClaims(ClaimsPrincipal principal, CalendarId calendarId) => new(principal.GetKeycloakSubject(), calendarId);
}

public sealed record GetCalendarResult(Calendar? Calendar, CalendarAccess Access);

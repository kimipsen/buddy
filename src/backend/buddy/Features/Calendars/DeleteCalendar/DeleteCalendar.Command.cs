using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record DeleteCalendar(KeycloakSubject Subject, CalendarId CalendarId)
{
    public static DeleteCalendar FromClaims(ClaimsPrincipal principal, CalendarId calendarId) => new(principal.GetKeycloakSubject(), calendarId);
}

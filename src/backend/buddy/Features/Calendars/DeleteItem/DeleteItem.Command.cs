using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record DeleteItem(KeycloakSubject Subject, CalendarId CalendarId, CalendarItemId ItemId)
{
    public static DeleteItem FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId) =>
        new(principal.GetKeycloakSubject(), calendarId, itemId);
}

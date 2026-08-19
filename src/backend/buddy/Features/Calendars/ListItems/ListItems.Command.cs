using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record ListItems(KeycloakSubject Subject, CalendarId CalendarId)
{
    public static ListItems FromClaims(ClaimsPrincipal principal, CalendarId calendarId) => new(principal.GetKeycloakSubject(), calendarId);
}

public sealed record ListItemsResult(IReadOnlyCollection<CalendarItem> Items, CalendarAccess Access);

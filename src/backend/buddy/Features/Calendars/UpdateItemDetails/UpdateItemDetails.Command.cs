using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record UpdateItemDetails(KeycloakSubject Subject, CalendarId CalendarId, CalendarItemId ItemId, string Title, Icon Icon, Color Color)
{
    public static UpdateItemDetails FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, string title, Icon icon, Color color) =>
        new(principal.GetKeycloakSubject(), calendarId, itemId, title, icon, color);
}

public sealed record UpdateItemResult(CalendarItem? Item, CalendarAccess Access, string? ValidationError = null);

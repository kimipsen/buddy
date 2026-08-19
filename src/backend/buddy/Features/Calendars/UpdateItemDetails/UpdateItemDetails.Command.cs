using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record UpdateItemDetails(UserId? UserId, CalendarId CalendarId, CalendarItemId ItemId, string Title, Icon Icon, Color Color)
{
    public static UpdateItemDetails FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, string title, Icon icon, Color color) =>
        new(principal.GetUserId(), calendarId, itemId, title, icon, color);
}

public sealed record UpdateItemResult(CalendarItem? Item, CalendarAccess Access, string? ValidationError = null);

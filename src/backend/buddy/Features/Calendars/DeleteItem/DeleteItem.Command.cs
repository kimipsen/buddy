using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record DeleteItem(UserId? UserId, CalendarId CalendarId, CalendarItemId ItemId)
{
    public static DeleteItem FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId) =>
        new(principal.GetUserId(), calendarId, itemId);
}

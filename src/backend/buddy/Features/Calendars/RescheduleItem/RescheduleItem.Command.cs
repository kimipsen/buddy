using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record RescheduleItem(UserId UserId, CalendarId CalendarId, CalendarItemId ItemId, ItemTiming Timing)
{
    public static RescheduleItem FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, ItemTiming timing) =>
        new(principal.GetRequiredUserId(), calendarId, itemId, timing);
}

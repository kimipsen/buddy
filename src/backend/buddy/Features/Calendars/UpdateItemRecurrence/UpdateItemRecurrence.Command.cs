using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record UpdateItemRecurrence(UserId UserId, CalendarId CalendarId, CalendarItemId ItemId, Recurrence Recurrence)
{
    public static UpdateItemRecurrence FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, Recurrence recurrence) =>
        new(principal.GetRequiredUserId(), calendarId, itemId, recurrence);
}

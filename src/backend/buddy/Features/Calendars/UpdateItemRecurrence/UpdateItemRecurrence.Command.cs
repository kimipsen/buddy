using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record UpdateItemRecurrence(UserId? UserId, CalendarId CalendarId, CalendarItemId ItemId, RecurrenceRule? Recurrence)
{
    public static UpdateItemRecurrence FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, RecurrenceRule? recurrence) =>
        new(principal.GetUserId(), calendarId, itemId, recurrence);
}

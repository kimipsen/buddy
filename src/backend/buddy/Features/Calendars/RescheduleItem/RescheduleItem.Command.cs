using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record RescheduleItem(UserId? UserId, CalendarId CalendarId, CalendarItemId ItemId, StartsAt? StartsAt, EndsAt? EndsAt, DueDate? DueDate, bool IsAllDay)
{
    public static RescheduleItem FromClaims(
        ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, StartsAt? startsAt, EndsAt? endsAt, DueDate? dueDate, bool isAllDay) =>
        new(principal.GetUserId(), calendarId, itemId, startsAt, endsAt, dueDate, isAllDay);
}

using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record RescheduleItem(UserId? UserId, CalendarId CalendarId, CalendarItemId ItemId, StartsAt? StartsAt, EndsAt? EndsAt, DueDate? DueDate)
{
    public static RescheduleItem FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, StartsAt? startsAt, EndsAt? endsAt, DueDate? dueDate) =>
        new(principal.GetUserId(), calendarId, itemId, startsAt, endsAt, dueDate);
}

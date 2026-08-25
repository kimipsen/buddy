using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record SetTaskCompletion(UserId? UserId, CalendarId CalendarId, CalendarItemId ItemId, DateOnly OccurrenceDate, bool IsCompleted)
{
    public static SetTaskCompletion FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, DateOnly occurrenceDate, bool isCompleted) =>
        new(principal.GetUserId(), calendarId, itemId, occurrenceDate, isCompleted);
}

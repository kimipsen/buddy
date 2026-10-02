using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

// Target is WholeTask for a plain task and Subtask for one subtask of a template-scheduled task --
// the two routes (.../completion and .../subtasks/{subtaskId}/completion) each build one, and
// SetTaskCompletionHandler rejects a target that doesn't fit the item's TaskSource.
public sealed record SetTaskCompletion(UserId UserId, CalendarId CalendarId, CalendarItemId ItemId, DateOnly OccurrenceDate, bool IsCompleted, CompletionTarget Target)
{
    public static SetTaskCompletion FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, DateOnly occurrenceDate, bool isCompleted, CompletionTarget target) =>
        new(principal.GetRequiredUserId(), calendarId, itemId, occurrenceDate, isCompleted, target);
}

using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

// SubtaskId is required when the target item is template-scheduled (CalendarItem.TaskTemplateId
// is set) and must be null otherwise -- see SetTaskCompletionHandler, which enforces both
// directions of that rule so the two completion modes never mix.
public sealed record SetTaskCompletion(UserId? UserId, CalendarId CalendarId, CalendarItemId ItemId, DateOnly OccurrenceDate, bool IsCompleted, Guid? SubtaskId = null)
{
    public static SetTaskCompletion FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, DateOnly occurrenceDate, bool isCompleted, Guid? subtaskId) =>
        new(principal.GetUserId(), calendarId, itemId, occurrenceDate, isCompleted, subtaskId);
}

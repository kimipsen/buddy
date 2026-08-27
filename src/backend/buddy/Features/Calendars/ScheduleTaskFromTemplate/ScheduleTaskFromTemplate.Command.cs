using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

// Schedules a new Task-kind CalendarItem whose subtasks come from a TaskLibrary template (see
// Features/TaskLibrary) instead of being entered by hand -- the calendar-item analog of
// CreateItem for the Task branch, minus the options that don't make sense for a template-backed
// task: no IsAllDay (a template-scheduled task is never all-day -- StartTime anchors subtask 1),
// no free-form subtask entry.
public sealed record ScheduleTaskFromTemplate(
    UserId? UserId,
    CalendarId CalendarId,
    Guid TaskTemplateId,
    DateOnly StartDate,
    TimeOnly StartTime,
    RecurrenceRule? Recurrence,
    UserId? AssignedTo,
    string Title,
    Icon? Icon,
    Color Color)
{
    public static ScheduleTaskFromTemplate FromClaims(
        ClaimsPrincipal principal,
        CalendarId calendarId,
        Guid taskTemplateId,
        DateOnly startDate,
        TimeOnly startTime,
        RecurrenceRule? recurrence,
        UserId? assignedTo,
        string title,
        Icon? icon,
        Color color) =>
        new(principal.GetUserId(), calendarId, taskTemplateId, startDate, startTime, recurrence, assignedTo, title, icon, color);
}

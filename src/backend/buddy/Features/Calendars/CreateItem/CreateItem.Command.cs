using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record CreateItem(
    UserId? UserId,
    CalendarId CalendarId,
    CalendarItemKind Kind,
    string Title,
    Icon? Icon,
    Color Color,
    StartsAt? StartsAt,
    EndsAt? EndsAt,
    DueDate? DueDate,
    bool IsAllDay,
    RecurrenceRule? Recurrence,
    UserId? AssignedTo = null)
{
    public static CreateItem FromClaims(
        ClaimsPrincipal principal,
        CalendarId calendarId,
        CalendarItemKind kind,
        string title,
        Icon? icon,
        Color color,
        StartsAt? startsAt,
        EndsAt? endsAt,
        DueDate? dueDate,
        bool isAllDay,
        RecurrenceRule? recurrence,
        UserId? assignedTo) =>
        new(principal.GetUserId(), calendarId, kind, title, icon, color, startsAt, endsAt, dueDate, isAllDay, recurrence, assignedTo);
}

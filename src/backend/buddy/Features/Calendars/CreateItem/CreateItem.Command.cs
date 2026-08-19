using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record CreateItem(
    KeycloakSubject Subject,
    CalendarId CalendarId,
    CalendarItemKind Kind,
    string Title,
    Icon Icon,
    Color Color,
    Period? Period,
    DueDate? DueDate,
    RecurrenceRule? Recurrence)
{
    public static CreateItem FromClaims(
        ClaimsPrincipal principal,
        CalendarId calendarId,
        CalendarItemKind kind,
        string title,
        Icon icon,
        Color color,
        Period? period,
        DueDate? dueDate,
        RecurrenceRule? recurrence) =>
        new(principal.GetKeycloakSubject(), calendarId, kind, title, icon, color, period, dueDate, recurrence);
}

public sealed record CreateItemResult(CalendarItem? Item, CalendarAccess Access, string? ValidationError = null);

using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record CreateItem(
    UserId UserId,
    CalendarId CalendarId,
    string Title,
    Icon? Icon,
    Color Color,
    NewItemSchedule Schedule,
    RecurrenceRule? Recurrence)
{
    public static CreateItem FromClaims(
        ClaimsPrincipal principal,
        CalendarId calendarId,
        string title,
        Icon? icon,
        Color color,
        NewItemSchedule schedule,
        RecurrenceRule? recurrence) =>
        new(principal.GetRequiredUserId(), calendarId, title, icon, color, schedule, recurrence);
}

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
    Recurrence Recurrence,
    ItemAlarms Alarms)
{
    public static CreateItem FromClaims(
        ClaimsPrincipal principal,
        CalendarId calendarId,
        string title,
        Icon? icon,
        Color color,
        NewItemSchedule schedule,
        Recurrence recurrence,
        ItemAlarms alarms) =>
        new(principal.GetRequiredUserId(), calendarId, title, icon, color, schedule, recurrence, alarms);
}

using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record RescheduleItem(KeycloakSubject Subject, CalendarId CalendarId, CalendarItemId ItemId, Period? Period, DueDate? DueDate)
{
    public static RescheduleItem FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, Period? period, DueDate? dueDate) =>
        new(principal.GetKeycloakSubject(), calendarId, itemId, period, dueDate);
}

using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record UpdateItemRecurrence(KeycloakSubject Subject, CalendarId CalendarId, CalendarItemId ItemId, RecurrenceRule? Recurrence)
{
    public static UpdateItemRecurrence FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, RecurrenceRule? recurrence) =>
        new(principal.GetKeycloakSubject(), calendarId, itemId, recurrence);
}

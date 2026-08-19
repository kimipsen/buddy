using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record RescheduleItem(KeycloakSubject Subject, CalendarId CalendarId, CalendarItemId ItemId, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, DateTimeOffset? DueAt)
{
    public static RescheduleItem FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, DateTimeOffset? startsAt, DateTimeOffset? endsAt, DateTimeOffset? dueAt) =>
        new(principal.GetKeycloakSubject(), calendarId, itemId, startsAt, endsAt, dueAt);
}

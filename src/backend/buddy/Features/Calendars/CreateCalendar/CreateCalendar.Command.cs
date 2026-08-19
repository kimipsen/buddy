using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record CreateCalendar(KeycloakSubject Subject, string Name, TimeZoneId TimeZoneId)
{
    public static CreateCalendar FromClaims(ClaimsPrincipal principal, string name, TimeZoneId timeZoneId) =>
        new(principal.GetKeycloakSubject(), name, timeZoneId);
}

public sealed record CreateCalendarResult(Calendar? Calendar, bool Unauthenticated = false, string? ValidationError = null);

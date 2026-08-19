using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record CreateCalendar(KeycloakSubject Subject, string Name)
{
    public static CreateCalendar FromClaims(ClaimsPrincipal principal, string name) => new(principal.GetKeycloakSubject(), name);
}

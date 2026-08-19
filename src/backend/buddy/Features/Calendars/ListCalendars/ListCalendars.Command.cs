using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record ListCalendars(KeycloakSubject Subject)
{
    public static ListCalendars FromClaims(ClaimsPrincipal principal) => new(principal.GetKeycloakSubject());
}

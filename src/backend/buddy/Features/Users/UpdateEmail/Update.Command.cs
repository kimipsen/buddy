using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record UpdateEmail(KeycloakSubject Subject, string Value)
{
    public static UpdateEmail FromClaims(ClaimsPrincipal principal, string value) => new(principal.GetKeycloakSubject(), value);
}

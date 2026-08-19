using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record UpdateName(KeycloakSubject Subject, Name Name)
{
    public static UpdateName FromClaims(ClaimsPrincipal principal, Name name) => new(principal.GetKeycloakSubject(), name);
}

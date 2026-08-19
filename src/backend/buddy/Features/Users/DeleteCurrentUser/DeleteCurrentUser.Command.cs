using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record DeleteUser(KeycloakSubject Subject)
{
    public static DeleteUser FromClaims(ClaimsPrincipal principal) => new(principal.GetKeycloakSubject());
}

using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record ResendEmailVerification(KeycloakSubject Subject)
{
    public static ResendEmailVerification FromClaims(ClaimsPrincipal principal) => new(principal.GetKeycloakSubject());
}

using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record VerifyEmail(KeycloakSubject Subject, string Token)
{
    public static VerifyEmail FromClaims(ClaimsPrincipal principal, string token) => new(principal.GetKeycloakSubject(), token);
}

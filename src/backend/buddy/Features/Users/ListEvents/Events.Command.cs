using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record GetUserEvents(KeycloakSubject Subject)
{
    public static GetUserEvents FromClaims(ClaimsPrincipal principal) => new(principal.GetKeycloakSubject());
}

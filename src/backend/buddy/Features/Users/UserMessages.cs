using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record GetOrCreateUser(KeycloakSubject Subject, string? Email, bool EmailVerified, string? UserName, Name Name)
{
    public static GetOrCreateUser FromClaims(ClaimsPrincipal principal)
    {
        var emailVerified = principal.FindFirstValue(Claims.EmailVerified) is { } emailVerifiedClaim
            && bool.TryParse(emailVerifiedClaim, out var verified)
            && verified;

        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue(Claims.Email);

        return new GetOrCreateUser(
            principal.GetKeycloakSubject(),
            email,
            emailVerified,
            principal.FindFirstValue(Claims.PreferredUsername),
            Name.New(principal.FindFirstValue(Claims.GivenName) ?? "", principal.FindFirstValue(Claims.FamilyName) ?? ""));
    }
}

public sealed record GetUserEvents(KeycloakSubject Subject)
{
    public static GetUserEvents FromClaims(ClaimsPrincipal principal) => new(principal.GetKeycloakSubject());
}

public sealed record DeleteUser(KeycloakSubject Subject)
{
    public static DeleteUser FromClaims(ClaimsPrincipal principal) => new(principal.GetKeycloakSubject());
}

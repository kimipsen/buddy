using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record GetOrCreateUser(KeycloakSubject Subject, string? Email, bool EmailVerified, string UserName, Name Name, string? AcceptLanguageHeader)
{
    public static GetOrCreateUser FromClaims(ClaimsPrincipal principal, string? acceptLanguageHeader)
    {
        var emailVerified = principal.FindFirstValue(Claims.EmailVerified) is { } emailVerifiedClaim
            && bool.TryParse(emailVerifiedClaim, out var verified)
            && verified;

        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue(Claims.Email);

        var subject = principal.GetKeycloakSubject();

        // A token without preferred_username still names a unique user: its subject.
        return new GetOrCreateUser(
            subject,
            email,
            emailVerified,
            principal.FindFirstValue(Claims.PreferredUsername) ?? subject.Value,
            Name.New(principal.FindFirstValue(Claims.GivenName) ?? "", principal.FindFirstValue(Claims.FamilyName) ?? ""),
            acceptLanguageHeader);
    }
}

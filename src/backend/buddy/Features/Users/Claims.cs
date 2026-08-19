using System.Security.Claims;

namespace buddy.Features.Users;

public static class Claims
{
    public const string KeycloakSubject = "sub";
    public const string Email = "email";
    public const string EmailVerified = "email_verified";
    public const string PreferredUsername = "preferred_username";
    public const string GivenName = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname";
    public const string FamilyName = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname";

    // Added to the principal by UserIdClaimsTransformation once the Keycloak subject has been
    // resolved to a backend UserId, so handlers don't each have to look it up themselves.
    public const string UserId = "buddy:user_id";
}

public static class ClaimsPrincipalExtensions
{
    public static KeycloakSubject GetKeycloakSubject(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(Claims.KeycloakSubject)
            ?? throw new UnauthorizedAccessException("Authenticated user is missing the Keycloak subject claim.");

        return KeycloakSubject.New(subject);
    }

    // Null when the authenticated Keycloak subject has no matching backend user yet (e.g. it
    // hasn't gone through GetOrCreateUser via GET /users/me).
    public static UserId? GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(Claims.UserId) is { } value ? new UserId(Guid.Parse(value)) : null;
}
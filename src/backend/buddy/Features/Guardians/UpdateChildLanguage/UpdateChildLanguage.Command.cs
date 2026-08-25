using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

// A guardian sets a child's language on the child's own User stream -- ChildId (from the route)
// plus the caller's own claims-derived UserId is the whole input, same shape as RevokeGuardianLink.
public sealed record UpdateChildLanguage(UserId? GuardianId, UserId ChildId, Language Language)
{
    public static UpdateChildLanguage FromClaims(ClaimsPrincipal principal, UserId childId, Language language) =>
        new(principal.GetUserId(), childId, language);
}

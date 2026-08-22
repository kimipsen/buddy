using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

// A guardian can only revoke their own link, never someone else's, so ChildId (from the route) plus
// the caller's own claims-derived UserId is the whole input -- no target-guardian parameter.
public sealed record RevokeGuardianLink(UserId? GuardianId, UserId ChildId)
{
    public static RevokeGuardianLink FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetUserId(), childId);
}

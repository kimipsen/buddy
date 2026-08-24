using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record ListGuardianInvites(UserId? UserId, UserId ChildId)
{
    public static ListGuardianInvites FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetUserId(), childId);
}

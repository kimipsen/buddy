using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record ListChildGuardians(UserId? CallerId, UserId ChildId)
{
    public static ListChildGuardians FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetUserId(), childId);
}

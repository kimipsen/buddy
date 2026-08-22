using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record ListMyGuardians(UserId? ChildId)
{
    public static ListMyGuardians FromClaims(ClaimsPrincipal principal) => new(principal.GetUserId());
}

using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record ListMyChildren(UserId? GuardianId)
{
    public static ListMyChildren FromClaims(ClaimsPrincipal principal) => new(principal.GetUserId());
}

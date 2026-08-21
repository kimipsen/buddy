using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record ListGroups(UserId? UserId)
{
    public static ListGroups FromClaims(ClaimsPrincipal principal) => new(principal.GetUserId());
}

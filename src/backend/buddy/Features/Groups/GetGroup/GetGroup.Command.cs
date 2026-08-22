using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record GetGroup(UserId? UserId, GroupId GroupId)
{
    public static GetGroup FromClaims(ClaimsPrincipal principal, GroupId groupId) =>
        new(principal.GetUserId(), groupId);
}

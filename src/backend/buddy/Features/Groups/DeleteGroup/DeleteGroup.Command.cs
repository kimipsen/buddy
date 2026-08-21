using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record DeleteGroup(UserId? UserId, GroupId GroupId)
{
    public static DeleteGroup FromClaims(ClaimsPrincipal principal, GroupId groupId) =>
        new(principal.GetUserId(), groupId);
}

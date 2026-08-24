using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record AddChildToGroup(UserId? UserId, GroupId GroupId, UserId ChildId)
{
    public static AddChildToGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, UserId childId) =>
        new(principal.GetUserId(), groupId, childId);
}

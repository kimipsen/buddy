using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record RemoveGroupMember(UserId? UserId, GroupId GroupId, UserId MemberId)
{
    public static RemoveGroupMember FromClaims(ClaimsPrincipal principal, GroupId groupId, UserId memberId) =>
        new(principal.GetUserId(), groupId, memberId);
}

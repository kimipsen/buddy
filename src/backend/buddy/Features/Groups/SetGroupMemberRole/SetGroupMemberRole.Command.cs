using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record SetGroupMemberRole(UserId? UserId, GroupId GroupId, UserId MemberId, GroupRole Role)
{
    public static SetGroupMemberRole FromClaims(ClaimsPrincipal principal, GroupId groupId, UserId memberId, GroupRole role) =>
        new(principal.GetUserId(), groupId, memberId, role);
}

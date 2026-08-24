using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record RevokeGroupInvite(UserId? UserId, GroupId GroupId, Guid InviteId)
{
    public static RevokeGroupInvite FromClaims(ClaimsPrincipal principal, GroupId groupId, Guid inviteId) =>
        new(principal.GetUserId(), groupId, inviteId);
}

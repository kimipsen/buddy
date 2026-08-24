using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record RevokeGuardianInvite(UserId? UserId, UserId ChildId, Guid InviteId)
{
    public static RevokeGuardianInvite FromClaims(ClaimsPrincipal principal, UserId childId, Guid inviteId) =>
        new(principal.GetUserId(), childId, inviteId);
}

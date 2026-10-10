using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Progress;

public sealed record ApproveRewardRequest(UserId UserId, UserId ChildId, RewardRequestId RequestId)
{
    public static ApproveRewardRequest FromClaims(ClaimsPrincipal principal, UserId childId, RewardRequestId requestId) =>
        new(principal.GetRequiredUserId(), childId, requestId);
}

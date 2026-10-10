using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Progress;

public sealed record DeclineRewardRequest(UserId UserId, UserId ChildId, RewardRequestId RequestId)
{
    public static DeclineRewardRequest FromClaims(ClaimsPrincipal principal, UserId childId, RewardRequestId requestId) =>
        new(principal.GetRequiredUserId(), childId, requestId);
}

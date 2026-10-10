using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Progress;

// The signed-in child withdraws one of their own pending requests.
public sealed record CancelRewardRequest(UserId ChildId, RewardRequestId RequestId)
{
    public static CancelRewardRequest FromClaims(ClaimsPrincipal principal, RewardRequestId requestId) => new(principal.GetRequiredUserId(), requestId);
}

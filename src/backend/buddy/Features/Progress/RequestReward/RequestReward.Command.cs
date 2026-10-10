using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Progress;

// The signed-in child asks for one of their own rewards. There is no childId: the caller's own
// progress stream is the only one this can reach.
public sealed record RequestReward(UserId ChildId, RewardId RewardId)
{
    public static RequestReward FromClaims(ClaimsPrincipal principal, RewardId rewardId) => new(principal.GetRequiredUserId(), rewardId);
}

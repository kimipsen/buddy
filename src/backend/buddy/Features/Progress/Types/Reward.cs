using buddy.Features.Users;

namespace buddy.Features.Progress;

// A guardian-defined reward a child can spend stars on. See docs/backend/analysis/reward-redemption.md.
public sealed record Reward(RewardId Id, string Name, string Icon, int Cost);

public enum RewardRequestStatus
{
    Pending,
    Approved,
    Declined,
    Cancelled
}

// A child's request for a reward. Name, icon and cost are copied from the catalog when the child
// asks, so a guardian editing or removing the reward meanwhile doesn't change what was asked for,
// and approving spends the cost the child saw.
public sealed record RewardRequest(
    RewardRequestId Id,
    RewardId RewardId,
    string Name,
    string Icon,
    int Cost,
    RewardRequestStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ResolvedAt,
    UserId? ResolvedBy);

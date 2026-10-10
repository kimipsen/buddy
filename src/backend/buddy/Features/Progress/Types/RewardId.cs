namespace buddy.Features.Progress;

// One entry in a child's reward catalog. Stable across ConfigureRewards saves, so a pending
// RewardRequest keeps pointing at the same reward after the guardian edits it.
public sealed record RewardId(Guid Value)
{
    public static RewardId New() => new(Guid.CreateVersion7());
}

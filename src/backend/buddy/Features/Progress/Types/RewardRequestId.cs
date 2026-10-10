namespace buddy.Features.Progress;

public sealed record RewardRequestId(Guid Value)
{
    public static RewardRequestId New() => new(Guid.CreateVersion7());
}

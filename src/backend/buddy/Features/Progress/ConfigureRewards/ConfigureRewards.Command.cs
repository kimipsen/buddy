using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Progress;

// Full replace of a child's reward catalog. A row without an Id is a new reward; a row with one
// keeps that reward's identity (and so any pending request's link to it).
public sealed record ConfigureRewards(UserId UserId, UserId ChildId, ImmutableArray<RewardDraft> Rewards)
{
    public static ConfigureRewards FromClaims(ClaimsPrincipal principal, UserId childId, ImmutableArray<RewardDraft> rewards) =>
        new(principal.GetRequiredUserId(), childId, rewards);
}

public sealed record RewardDraft(RewardId? Id, string Name, string Icon, int Cost);

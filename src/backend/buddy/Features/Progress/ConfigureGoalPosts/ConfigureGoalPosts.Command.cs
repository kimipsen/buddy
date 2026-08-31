using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Progress;

public sealed record ConfigureGoalPosts(UserId? UserId, UserId ChildId, ImmutableArray<GoalPost> GoalPosts)
{
    public static ConfigureGoalPosts FromClaims(ClaimsPrincipal principal, UserId childId, ImmutableArray<GoalPost> goalPosts) =>
        new(principal.GetUserId(), childId, goalPosts);
}

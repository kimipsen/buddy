using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Features.Mealplans;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record UpdateMealplanPermissionPolicy(UserId? UserId, GroupId GroupId, ImmutableDictionary<GroupRole, MealplanAccessTier> Policy)
{
    public static UpdateMealplanPermissionPolicy FromClaims(ClaimsPrincipal principal, GroupId groupId, ImmutableDictionary<GroupRole, MealplanAccessTier> policy) =>
        new(principal.GetUserId(), groupId, policy);
}

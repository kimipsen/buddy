using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ArchiveMealForGroup(UserId? UserId, GroupId GroupId, MealId MealId)
{
    public static ArchiveMealForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, MealId mealId) =>
        new(principal.GetUserId(), groupId, mealId);
}

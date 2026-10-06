using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record RevertMealPlanImport(UserId UserId, UserId ChildId, MealPlanImportId ImportId)
{
    public static RevertMealPlanImport FromClaims(ClaimsPrincipal principal, UserId childId, MealPlanImportId importId) =>
        new(principal.GetRequiredUserId(), childId, importId);
}

public sealed record RevertMealPlanImportForGroup(UserId UserId, GroupId GroupId, MealPlanImportId ImportId)
{
    public static RevertMealPlanImportForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, MealPlanImportId importId) =>
        new(principal.GetRequiredUserId(), groupId, importId);
}

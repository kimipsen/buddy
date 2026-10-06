using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record PreviewMealPlanImport(UserId UserId, UserId ChildId, string Text, string Format, MealPlanImportOptions Options)
{
    public static PreviewMealPlanImport FromClaims(ClaimsPrincipal principal, UserId childId, string text, string format, MealPlanImportOptions options) =>
        new(principal.GetRequiredUserId(), childId, text, format, options);
}

public sealed record PreviewMealPlanImportForGroup(UserId UserId, GroupId GroupId, string Text, string Format, MealPlanImportOptions Options)
{
    public static PreviewMealPlanImportForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, string text, string format, MealPlanImportOptions options) =>
        new(principal.GetRequiredUserId(), groupId, text, format, options);
}

using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record CreateMealFromRecipe(UserId UserId, UserId ChildId, Guid RecipeId, Icon Icon, Color Color)
{
    public static CreateMealFromRecipe FromClaims(ClaimsPrincipal principal, UserId childId, Guid recipeId, Icon icon, Color color) =>
        new(principal.GetRequiredUserId(), childId, recipeId, icon, color);
}

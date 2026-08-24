using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ListMealPlanForGroup(UserId? UserId, GroupId GroupId, DateOnly From, DateOnly To)
{
    public static ListMealPlanForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, DateOnly from, DateOnly to) =>
        new(principal.GetUserId(), groupId, from, to);
}

using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record StartAiSession(
    UserId? UserId,
    UserId ChildId,
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<MealSlot> RequestedSlots,
    IReadOnlyCollection<MealId> MustIncludeMealIds,
    string? Notes)
{
    public static StartAiSession FromClaims(
        ClaimsPrincipal principal, UserId childId, DateOnly from, DateOnly to,
        IReadOnlyCollection<MealSlot> requestedSlots, IReadOnlyCollection<MealId> mustIncludeMealIds, string? notes) =>
        new(principal.GetUserId(), childId, from, to, requestedSlots, mustIncludeMealIds, notes);
}

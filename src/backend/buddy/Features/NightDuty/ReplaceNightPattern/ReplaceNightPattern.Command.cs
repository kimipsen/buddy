using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// EffectiveFrom null = "from today" on the child's wall clock (resolved by the handler).
public sealed record ReplaceNightPattern(
    UserId UserId, UserId ChildId, DateOnly? EffectiveFrom, int CycleWeeks, DateOnly AnchorMonday, IReadOnlyList<NightPatternDay> Days)
{
    public static ReplaceNightPattern FromClaims(
        ClaimsPrincipal principal, UserId childId, DateOnly? effectiveFrom, int cycleWeeks, DateOnly anchorMonday, IReadOnlyList<NightPatternDay> days) =>
        new(principal.GetRequiredUserId(), childId, effectiveFrom, cycleWeeks, anchorMonday, days);
}

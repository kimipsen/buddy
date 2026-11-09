using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// A list of (date, override) pairs rather than one value over a range, so a switch between two
// guardians -- two dates, two different values -- is one request and one append
// (docs/backend/analysis/night-duty.md, Question 5).
public sealed record SetNightOverrides(UserId UserId, UserId ChildId, IReadOnlyList<NightOverrideItem> Nights)
{
    public static SetNightOverrides FromClaims(ClaimsPrincipal principal, UserId childId, IReadOnlyList<NightOverrideItem> nights) =>
        new(principal.GetRequiredUserId(), childId, nights);
}

public sealed record NightOverrideItem(DateOnly Date, NightOverride Override);

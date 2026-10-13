using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

public sealed record ConfigureTransitionWarnings(
    UserId UserId,
    UserId ChildId,
    bool Enabled,
    ImmutableArray<int> LeadMinutes,
    ImmutableArray<WarningKind> Kinds,
    QuietHours? QuietHours)
{
    public static ConfigureTransitionWarnings FromClaims(
        ClaimsPrincipal principal, UserId childId, bool enabled, IEnumerable<int> leadMinutes, IEnumerable<WarningKind> kinds, QuietHours? quietHours) =>
        new(principal.GetRequiredUserId(), childId, enabled, [.. leadMinutes], [.. kinds], quietHours);
}

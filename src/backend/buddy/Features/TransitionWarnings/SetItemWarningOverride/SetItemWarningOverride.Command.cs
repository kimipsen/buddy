using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

// LeadMinutes empty: never warn for this item.
public sealed record SetItemWarningOverride(UserId UserId, UserId ChildId, CalendarItemId ItemId, ImmutableArray<int> LeadMinutes)
{
    public static SetItemWarningOverride FromClaims(ClaimsPrincipal principal, UserId childId, CalendarItemId itemId, IEnumerable<int> leadMinutes) =>
        new(principal.GetRequiredUserId(), childId, itemId, [.. leadMinutes]);
}

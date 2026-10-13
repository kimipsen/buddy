using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

public sealed record ClearItemWarningOverride(UserId UserId, UserId ChildId, CalendarItemId ItemId)
{
    public static ClearItemWarningOverride FromClaims(ClaimsPrincipal principal, UserId childId, CalendarItemId itemId) =>
        new(principal.GetRequiredUserId(), childId, itemId);
}

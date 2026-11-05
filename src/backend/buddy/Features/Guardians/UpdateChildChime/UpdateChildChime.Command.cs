using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

// A guardian allows or mutes the child's time's-up chime on the child's own User stream
// (docs/frontend/analysis/child-timer-preferences.md, Question 3) -- same shape as
// UpdateChildVisualTimers. A boolean has no invalid value, so there is no validator.
public sealed record UpdateChildChime(UserId GuardianId, UserId ChildId, bool AllowChime)
{
    public static UpdateChildChime FromClaims(ClaimsPrincipal principal, UserId childId, bool allowChime) =>
        new(principal.GetRequiredUserId(), childId, allowChime);
}

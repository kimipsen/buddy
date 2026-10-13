using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

// A guardian turns a child's visual time-left timers on or off on the child's own User stream --
// same shape as UpdateChildLanguage. A boolean has no invalid value, so there is no validator.
public sealed record UpdateChildVisualTimers(UserId GuardianId, UserId ChildId, bool ShowVisualTimers)
{
    public static UpdateChildVisualTimers FromClaims(ClaimsPrincipal principal, UserId childId, bool showVisualTimers) =>
        new(principal.GetRequiredUserId(), childId, showVisualTimers);
}

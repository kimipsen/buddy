using System.Security.Claims;

namespace buddy.Features.Users;

// The signed-in user (in practice a child) sets how their own visual timers look and sound
// (docs/frontend/analysis/child-timer-preferences.md, Question 3) -- same shape as UpdateLanguage:
// /users/me only ever writes the caller's own stream, so there's no GuardianLink to check.
public sealed record UpdateTimerPreferences(UserId UserId, TimerPreferences TimerPreferences)
{
    public static UpdateTimerPreferences FromClaims(ClaimsPrincipal principal, TimerPreferences timerPreferences) =>
        new(principal.GetRequiredUserId(), timerPreferences);
}

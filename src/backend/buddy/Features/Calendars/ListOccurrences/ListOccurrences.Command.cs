using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

// IncludeSkipped returns skipped occurrences too, flagged IsSkipped, so the guardian agenda can
// offer to restore them; every other caller leaves it off and never sees them.
public sealed record ListOccurrences(UserId UserId, CalendarId CalendarId, DateOnly From, DateOnly To, bool IncludeSkipped = false)
{
    public static ListOccurrences FromClaims(ClaimsPrincipal principal, CalendarId calendarId, DateOnly from, DateOnly to, bool includeSkipped = false) =>
        new(principal.GetRequiredUserId(), calendarId, from, to, includeSkipped);
}

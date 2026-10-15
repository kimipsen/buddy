using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

// IsSkipped true skips one occurrence of a repeating item, false restores it
// (docs/backend/analysis/skip-occurrences.md).
public sealed record SetOccurrenceSkip(UserId UserId, CalendarId CalendarId, CalendarItemId ItemId, DateOnly OccurrenceDate, bool IsSkipped)
{
    public static SetOccurrenceSkip FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, DateOnly occurrenceDate, bool isSkipped) =>
        new(principal.GetRequiredUserId(), calendarId, itemId, occurrenceDate, isSkipped);
}

using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record ListOccurrences(KeycloakSubject Subject, CalendarId CalendarId, DateOnly From, DateOnly To)
{
    public static ListOccurrences FromClaims(ClaimsPrincipal principal, CalendarId calendarId, DateOnly from, DateOnly to) =>
        new(principal.GetKeycloakSubject(), calendarId, from, to);
}

public sealed record CalendarItemOccurrence(
    CalendarItemId ItemId,
    CalendarItemKind Kind,
    string Title,
    string Icon,
    string Color,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset? DueAt,
    Guid CreatedBy,
    Guid LastModifiedBy);

public sealed record ListOccurrencesResult(IReadOnlyCollection<CalendarItemOccurrence> Occurrences, CalendarAccess Access, string? ValidationError = null);

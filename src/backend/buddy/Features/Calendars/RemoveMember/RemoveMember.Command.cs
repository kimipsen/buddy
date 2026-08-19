using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record RemoveMember(KeycloakSubject Subject, CalendarId CalendarId, UserId MemberId)
{
    public static RemoveMember FromClaims(ClaimsPrincipal principal, CalendarId calendarId, UserId memberId) =>
        new(principal.GetKeycloakSubject(), calendarId, memberId);
}

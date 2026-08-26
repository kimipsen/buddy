using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record ListAssignableMembers(UserId? UserId, CalendarId CalendarId)
{
    public static ListAssignableMembers FromClaims(ClaimsPrincipal principal, CalendarId calendarId) =>
        new(principal.GetUserId(), calendarId);
}

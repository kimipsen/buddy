using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record SetMemberRole(UserId? UserId, CalendarId CalendarId, UserId MemberId, CalendarRole Role)
{
    public static SetMemberRole FromClaims(ClaimsPrincipal principal, CalendarId calendarId, UserId memberId, CalendarRole role) =>
        new(principal.GetUserId(), calendarId, memberId, role);
}

using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record TransferCalendarToGroup(UserId? UserId, CalendarId CalendarId, GroupId NewGroupId)
{
    public static TransferCalendarToGroup FromClaims(ClaimsPrincipal principal, CalendarId calendarId, GroupId newGroupId) =>
        new(principal.GetUserId(), calendarId, newGroupId);
}

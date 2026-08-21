using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record CreateCalendar(UserId? UserId, string Name, TimeZoneId TimeZoneId, GroupId? GroupId = null)
{
    public static CreateCalendar FromClaims(ClaimsPrincipal principal, string name, TimeZoneId timeZoneId, GroupId? groupId = null) =>
        new(principal.GetUserId(), name, timeZoneId, groupId);
}

public sealed record CreateCalendarResult(Calendar? Calendar, bool Unauthenticated = false, bool Forbidden = false, string? ValidationError = null);

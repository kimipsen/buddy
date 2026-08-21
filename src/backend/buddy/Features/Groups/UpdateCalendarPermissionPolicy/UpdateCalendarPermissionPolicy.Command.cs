using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record UpdateCalendarPermissionPolicy(UserId? UserId, GroupId GroupId, ImmutableDictionary<GroupRole, CalendarRole> Policy)
{
    public static UpdateCalendarPermissionPolicy FromClaims(ClaimsPrincipal principal, GroupId groupId, ImmutableDictionary<GroupRole, CalendarRole> policy) =>
        new(principal.GetUserId(), groupId, policy);
}

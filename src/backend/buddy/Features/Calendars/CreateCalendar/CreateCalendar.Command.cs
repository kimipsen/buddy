using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record CreateCalendar(UserId? UserId, string Name, TimeZoneId TimeZoneId, GroupId? GroupId = null)
{
    public static CreateCalendar FromClaims(ClaimsPrincipal principal, string name, TimeZoneId timeZoneId, GroupId? groupId = null) =>
        new(principal.GetUserId(), name, timeZoneId, groupId);
}

// Distinct from the shared Result<T>: unlike every other calendar endpoint, there's no existing
// resource here to hide behind an ambiguous 404, so an unauthenticated caller genuinely gets a 401
// rather than collapsing into NotFound.
public union CreateCalendarOutcome(CreateCalendarOutcome.Success, CreateCalendarOutcome.Unauthenticated, CreateCalendarOutcome.Forbidden, CreateCalendarOutcome.Validation)
{
    public sealed record Success(Calendar Calendar);
    public sealed record Unauthenticated;
    public sealed record Forbidden;
    public sealed record Validation(string Message);
}

using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

// A guardian sets a child's time zone on the child's own User stream -- ChildId (from the route)
// plus the caller's own claims-derived UserId is the whole input, same shape as UpdateChildLanguage.
public sealed record UpdateChildTimeZone(UserId? GuardianId, UserId ChildId, TimeZoneId TimeZoneId)
{
    public static UpdateChildTimeZone FromClaims(ClaimsPrincipal principal, UserId childId, TimeZoneId timeZoneId) =>
        new(principal.GetUserId(), childId, timeZoneId);
}

using System.Security.Claims;

using buddy.Features.Calendars;

namespace buddy.Features.Users;

public sealed record UpdateTimeZone(UserId? UserId, TimeZoneId TimeZoneId)
{
    public static UpdateTimeZone FromClaims(ClaimsPrincipal principal, TimeZoneId timeZoneId) => new(principal.GetUserId(), timeZoneId);
}

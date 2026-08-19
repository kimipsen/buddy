using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record CreateCalendar(UserId? UserId, string Name, TimeZoneId TimeZoneId)
{
    public static CreateCalendar FromClaims(ClaimsPrincipal principal, string name, TimeZoneId timeZoneId) =>
        new(principal.GetUserId(), name, timeZoneId);
}

public sealed record CreateCalendarResult(Calendar? Calendar, bool Unauthenticated = false, string? ValidationError = null);

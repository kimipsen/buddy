using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record ListIcalTokens(UserId? UserId, CalendarId CalendarId)
{
    public static ListIcalTokens FromClaims(ClaimsPrincipal principal, CalendarId calendarId) => new(principal.GetUserId(), calendarId);
}

// Never exposes the hash -- just enough for the owner to recognize which token to revoke.
public sealed record IcalTokenSummary(Guid TokenId, DateTimeOffset IssuedAt);

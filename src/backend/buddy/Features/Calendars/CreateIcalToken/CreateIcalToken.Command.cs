using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record CreateIcalToken(UserId? UserId, CalendarId CalendarId)
{
    public static CreateIcalToken FromClaims(ClaimsPrincipal principal, CalendarId calendarId) => new(principal.GetUserId(), calendarId);
}

// Token is the plaintext subscription secret -- returned exactly once, on creation, and never again.
public sealed record IssuedIcalToken(IcalTokenId TokenId, string Token);

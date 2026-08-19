using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record RevokeIcalToken(UserId? UserId, CalendarId CalendarId, IcalTokenId TokenId)
{
    public static RevokeIcalToken FromClaims(ClaimsPrincipal principal, CalendarId calendarId, IcalTokenId tokenId) =>
        new(principal.GetUserId(), calendarId, tokenId);
}

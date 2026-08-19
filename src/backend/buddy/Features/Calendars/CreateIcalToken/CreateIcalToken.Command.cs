using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record CreateIcalToken(UserId? UserId, CalendarId CalendarId)
{
    public static CreateIcalToken FromClaims(ClaimsPrincipal principal, CalendarId calendarId) => new(principal.GetUserId(), calendarId);
}

public sealed record CreateIcalTokenResult(IcalTokenId? TokenId, string? Token, CalendarAccess Access);

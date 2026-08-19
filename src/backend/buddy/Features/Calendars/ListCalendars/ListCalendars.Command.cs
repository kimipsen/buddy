using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record ListCalendars(UserId? UserId)
{
    public static ListCalendars FromClaims(ClaimsPrincipal principal) => new(principal.GetUserId());
}

using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record ListItems(UserId? UserId, CalendarId CalendarId)
{
    public static ListItems FromClaims(ClaimsPrincipal principal, CalendarId calendarId) => new(principal.GetUserId(), calendarId);
}

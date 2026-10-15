using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record UpdateItemAlarms(UserId UserId, CalendarId CalendarId, CalendarItemId ItemId, ItemAlarms Alarms)
{
    public static UpdateItemAlarms FromClaims(ClaimsPrincipal principal, CalendarId calendarId, CalendarItemId itemId, ItemAlarms alarms) =>
        new(principal.GetRequiredUserId(), calendarId, itemId, alarms);
}

using System.Security.Claims;

using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

// GroupId is required -- a calendar is always group-owned now. CalendarOwner.User and the plain
// CalendarCreated event stay supported in Calendar.Rehydrate/CalendarAuthorization purely for
// calendars created before this change; no new one is ever produced.
public sealed record CreateCalendar(UserId UserId, string Name, TimeZoneId TimeZoneId, GroupId GroupId, Icon? Icon)
{
    public static CreateCalendar FromClaims(ClaimsPrincipal principal, string name, TimeZoneId timeZoneId, GroupId groupId, Icon? icon) =>
        new(principal.GetRequiredUserId(), name, timeZoneId, groupId, icon);
}

// Distinct from the shared Result<T>: there's no existing resource here to hide behind an
// ambiguous 404, so creation has no NotFound outcome.
public union CreateCalendarOutcome(CreateCalendarOutcome.Success, CreateCalendarOutcome.Forbidden, CreateCalendarOutcome.Validation)
{
    public sealed record Success(Calendar Calendar);
    public sealed record Forbidden;
    public sealed record Validation(ValidationProblem Problem);
}

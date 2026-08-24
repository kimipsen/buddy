using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class CreateCalendarHandler
{
    public static async Task<CreateCalendarOutcome> Handle(CreateCalendar command, ICalendarEventStore calendars, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (!TimeZoneResolution.IsValid(command.TimeZoneId))
        {
            return new CreateCalendarOutcome.Validation($"'{command.TimeZoneId.Value}' is not a recognized IANA time zone identifier.");
        }

        if (command.UserId is not { } ownerId)
        {
            return new CreateCalendarOutcome.Unauthenticated();
        }

        var group = Group.Rehydrate(await groups.ReadAsync(command.GroupId, cancellationToken));

        // A missing/unmanaged GroupId (including an omitted one, which binds to an empty Guid)
        // collapses into the same Forbidden this already returned for "not a manager of this
        // group" -- there's no separate NotFound case on this outcome, since unlike every other
        // calendar endpoint there's no existing resource yet to hide behind an ambiguous 404.
        if (GroupAuthorization.CheckManage(group, ownerId) != GroupAccess.Allowed)
        {
            return new CreateCalendarOutcome.Forbidden();
        }

        var calendarId = CalendarId.New();
        CalendarEvent created = new CalendarCreatedForGroup(calendarId, command.GroupId, command.Name, command.TimeZoneId, DateTimeOffset.UtcNow);

        var events = await calendars.CreateAsync(calendarId, [created], cancellationToken);

        return new CreateCalendarOutcome.Success(Calendar.Rehydrate(events)!);
    }
}

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

        var calendarId = CalendarId.New();
        CalendarEvent created;

        if (command.GroupId is { } groupId)
        {
            var group = Group.Rehydrate(await groups.ReadAsync(groupId, cancellationToken));

            if (GroupAuthorization.CheckManage(group, ownerId) != GroupAccess.Allowed)
            {
                return new CreateCalendarOutcome.Forbidden();
            }

            created = new CalendarCreatedForGroup(calendarId, groupId, command.Name, command.TimeZoneId, DateTimeOffset.UtcNow);
        }
        else
        {
            created = new CalendarCreated(calendarId, ownerId, command.Name, command.TimeZoneId, DateTimeOffset.UtcNow);
        }

        var events = await calendars.CreateAsync(calendarId, [created], cancellationToken);

        return new CreateCalendarOutcome.Success(Calendar.Rehydrate(events)!);
    }
}

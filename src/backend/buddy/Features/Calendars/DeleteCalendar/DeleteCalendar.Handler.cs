using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class DeleteCalendarHandler
{
    public static async Task<CalendarAccess> Handle(DeleteCalendar command, ICalendarEventStore calendars, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return CalendarAccess.NotFound;
        }

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access;
        }

        await calendars.AppendAsync(command.CalendarId, [new CalendarDeleted(command.CalendarId, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return CalendarAccess.Allowed;
    }
}

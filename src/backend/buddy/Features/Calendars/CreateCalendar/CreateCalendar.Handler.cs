using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class CreateCalendarHandler
{
    public static async Task<Calendar?> Handle(CreateCalendar command, IUserEventStore users, ICalendarEventStore calendars, CancellationToken cancellationToken)
    {
        var ownerId = await users.FindUserIdAsync(command.Subject, cancellationToken);

        if (ownerId is null)
        {
            return null;
        }

        var calendarId = CalendarId.New();
        var created = new CalendarCreated(calendarId, ownerId, command.Name, DateTimeOffset.UtcNow);

        var events = await calendars.CreateAsync(calendarId, [created], cancellationToken);

        return Calendar.Rehydrate(events);
    }
}

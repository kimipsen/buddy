using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class CreateCalendarHandler
{
    public static async Task<CreateCalendarResult> Handle(CreateCalendar command, ICalendarEventStore calendars, CancellationToken cancellationToken)
    {
        if (!TimeZoneResolution.IsValid(command.TimeZoneId))
        {
            return new CreateCalendarResult(null, ValidationError: $"'{command.TimeZoneId.Value}' is not a recognized IANA time zone identifier.");
        }

        if (command.UserId is not { } ownerId)
        {
            return new CreateCalendarResult(null, Unauthenticated: true);
        }

        var calendarId = CalendarId.New();
        var created = new CalendarCreated(calendarId, ownerId, command.Name, command.TimeZoneId, DateTimeOffset.UtcNow);

        var events = await calendars.CreateAsync(calendarId, [created], cancellationToken);

        return new CreateCalendarResult(Calendar.Rehydrate(events));
    }
}

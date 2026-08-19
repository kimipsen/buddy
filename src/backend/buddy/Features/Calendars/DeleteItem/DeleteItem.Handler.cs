using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class DeleteItemHandler
{
    public static async Task<CalendarAccess> Handle(
        DeleteItem command,
        IUserEventStore users,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        CancellationToken cancellationToken)
    {
        var userId = await users.FindUserIdAsync(command.Subject, cancellationToken);

        if (userId is null)
        {
            return CalendarAccess.NotFound;
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = CalendarAuthorization.CheckContribute(calendar, userId);

        if (access != CalendarAccess.Allowed)
        {
            return access;
        }

        var itemEvents = await items.ReadAsync(command.ItemId, cancellationToken);
        var item = CalendarItem.Rehydrate(itemEvents);

        if (item is null || item.IsDeleted || item.CalendarId != command.CalendarId)
        {
            return CalendarAccess.NotFound;
        }

        await items.AppendAsync(command.ItemId, [new ItemDeleted(command.ItemId, DateTimeOffset.UtcNow)], cancellationToken);

        return CalendarAccess.Allowed;
    }
}

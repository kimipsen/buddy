using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class DeleteItemHandler
{
    public static async Task<CalendarAccess> Handle(
        DeleteItem command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return CalendarAccess.NotFound;
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, cancellationToken);

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

        await items.AppendAsync(command.ItemId, [new ItemDeleted(command.ItemId, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return CalendarAccess.Allowed;
    }
}

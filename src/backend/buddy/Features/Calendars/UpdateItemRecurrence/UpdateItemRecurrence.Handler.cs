using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class UpdateItemRecurrenceHandler
{
    public static async Task<UpdateItemResult> Handle(
        UpdateItemRecurrence command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (command.Recurrence is { IntervalCount: < 1 })
        {
            return new UpdateItemResult(null, CalendarAccess.Allowed, "Recurrence interval count must be at least 1.");
        }

        if (command.UserId is not { } userId)
        {
            return new UpdateItemResult(null, CalendarAccess.NotFound);
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return new UpdateItemResult(null, access);
        }

        var itemEvents = await items.ReadAsync(command.ItemId, cancellationToken);
        var item = CalendarItem.Rehydrate(itemEvents);

        if (item is null || item.IsDeleted || item.CalendarId != command.CalendarId)
        {
            return new UpdateItemResult(null, CalendarAccess.NotFound);
        }

        if (item.Recurrence == command.Recurrence)
        {
            return new UpdateItemResult(item, CalendarAccess.Allowed);
        }

        await items.AppendAsync(
            command.ItemId,
            [new RecurrenceUpdated(command.ItemId, item.Recurrence, command.Recurrence, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new UpdateItemResult(item with { Recurrence = command.Recurrence, LastModifiedBy = userId }, CalendarAccess.Allowed);
    }
}

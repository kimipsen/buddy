using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class UpdateItemRecurrenceHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        UpdateItemRecurrence command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (command.Recurrence is { IntervalCount: < 1 })
        {
            return new Result<CalendarItem>.Validation("Recurrence interval count must be at least 1.");
        }

        if (command.UserId is not { } userId)
        {
            return new Result<CalendarItem>.NotFound();
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access == CalendarAccess.Forbidden ? new Result<CalendarItem>.Forbidden() : new Result<CalendarItem>.NotFound();
        }

        var itemEvents = await items.ReadAsync(command.ItemId, cancellationToken);
        var item = CalendarItem.Rehydrate(itemEvents);

        if (item is null || item.IsDeleted || item.CalendarId != command.CalendarId)
        {
            return new Result<CalendarItem>.NotFound();
        }

        if (item.Recurrence == command.Recurrence)
        {
            return new Result<CalendarItem>.Success(item);
        }

        await items.AppendAsync(
            command.ItemId,
            [new RecurrenceUpdated(command.ItemId, item.Recurrence, command.Recurrence, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<CalendarItem>.Success(item with { Recurrence = command.Recurrence, LastModifiedBy = userId });
    }
}

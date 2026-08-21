using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class RescheduleItemHandler
{
    public static async Task<UpdateItemResult> Handle(
        RescheduleItem command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
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

        var now = DateTimeOffset.UtcNow;

        if (item.Kind == CalendarItemKind.Event)
        {
            if (command.StartsAt is null || command.EndsAt is null)
            {
                return new UpdateItemResult(null, CalendarAccess.Allowed, "An event requires both a start and an end time.");
            }

            if (!Period.TryCreate(command.StartsAt, command.EndsAt, out var period))
            {
                return new UpdateItemResult(null, CalendarAccess.Allowed, "An event's end time must be after its start time.");
            }

            await items.AppendAsync(
                command.ItemId,
                [new EventRescheduled(command.ItemId, item.Period!, period!, userId, now)],
                cancellationToken);

            return new UpdateItemResult(item with { Period = period, LastModifiedBy = userId }, CalendarAccess.Allowed);
        }

        if (command.DueDate is null)
        {
            return new UpdateItemResult(null, CalendarAccess.Allowed, "A task requires a due date.");
        }

        await items.AppendAsync(
            command.ItemId,
            [new TaskRescheduled(command.ItemId, item.DueDate!, command.DueDate, userId, now)],
            cancellationToken);

        return new UpdateItemResult(item with { DueDate = command.DueDate, LastModifiedBy = userId }, CalendarAccess.Allowed);
    }
}

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class RescheduleItemHandler
{
    public static async Task<UpdateItemResult> Handle(
        RescheduleItem command,
        IUserEventStore users,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        CancellationToken cancellationToken)
    {
        var userId = await users.FindUserIdAsync(command.Subject, cancellationToken);

        if (userId is null)
        {
            return new UpdateItemResult(null, CalendarAccess.NotFound);
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = CalendarAuthorization.CheckContribute(calendar, userId);

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
            if (command.Period is null)
            {
                return new UpdateItemResult(null, CalendarAccess.Allowed, "An event requires both a start and an end time.");
            }

            if (command.Period.EndsAt.Date.ToDateTime(command.Period.EndsAt.Time) <= command.Period.StartsAt.Date.ToDateTime(command.Period.StartsAt.Time))
            {
                return new UpdateItemResult(null, CalendarAccess.Allowed, "An event's end time must be after its start time.");
            }

            await items.AppendAsync(
                command.ItemId,
                [new EventRescheduled(command.ItemId, item.Period!, command.Period, userId, now)],
                cancellationToken);

            return new UpdateItemResult(item with { Period = command.Period, LastModifiedBy = userId }, CalendarAccess.Allowed);
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

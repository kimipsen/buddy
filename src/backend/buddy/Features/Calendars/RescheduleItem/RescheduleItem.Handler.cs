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
            if (command.StartsAt is null || command.EndsAt is null)
            {
                return new UpdateItemResult(null, CalendarAccess.Allowed, "An event requires both a start and an end time.");
            }

            if (command.EndsAt <= command.StartsAt)
            {
                return new UpdateItemResult(null, CalendarAccess.Allowed, "An event's end time must be after its start time.");
            }

            await items.AppendAsync(
                command.ItemId,
                [new EventRescheduled(command.ItemId, item.StartsAt!.Value, item.EndsAt!.Value, command.StartsAt.Value, command.EndsAt.Value, now)],
                cancellationToken);

            return new UpdateItemResult(item with { StartsAt = command.StartsAt, EndsAt = command.EndsAt }, CalendarAccess.Allowed);
        }

        if (command.DueAt is null)
        {
            return new UpdateItemResult(null, CalendarAccess.Allowed, "A task requires a due date.");
        }

        await items.AppendAsync(
            command.ItemId,
            [new TaskRescheduled(command.ItemId, item.DueAt!.Value, command.DueAt.Value, now)],
            cancellationToken);

        return new UpdateItemResult(item with { DueAt = command.DueAt }, CalendarAccess.Allowed);
    }
}

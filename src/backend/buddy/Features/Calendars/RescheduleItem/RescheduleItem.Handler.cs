using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class RescheduleItemHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        RescheduleItem command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
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

        var now = DateTimeOffset.UtcNow;

        if (item.Kind == CalendarItemKind.Event)
        {
            if (command.StartsAt is null || command.EndsAt is null)
            {
                return new Result<CalendarItem>.Validation("An event requires both a start and an end time.");
            }

            var periodResult = Period.TryCreate(command.StartsAt, command.EndsAt);

            if (periodResult is not Result<Period>.Success(var period))
            {
                return new Result<CalendarItem>.Validation(periodResult is Result<Period>.Validation(var message) ? message : "Invalid period.");
            }

            await items.AppendAsync(
                command.ItemId,
                [new EventRescheduled(command.ItemId, item.Period!, period, userId, now)],
                cancellationToken);

            return new Result<CalendarItem>.Success(item with { Period = period, LastModifiedBy = userId });
        }

        if (command.DueDate is null)
        {
            return new Result<CalendarItem>.Validation("A task requires a due date.");
        }

        await items.AppendAsync(
            command.ItemId,
            [new TaskRescheduled(command.ItemId, item.DueDate!, command.DueDate, userId, now)],
            cancellationToken);

        return new Result<CalendarItem>.Success(item with { DueDate = command.DueDate, LastModifiedBy = userId });
    }
}

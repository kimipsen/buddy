using System.Diagnostics;

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
            return access.ToDeniedResult<CalendarItem>();
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

            if (periodResult is not PeriodValidationResult.Valid(var period))
            {
                return new Result<CalendarItem>.Validation(periodResult switch
                {
                    PeriodValidationResult.Invalid(var message) => message,
                    PeriodValidationResult.Valid => throw new UnreachableException("Already excluded by the enclosing check."),
                });
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

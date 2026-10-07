using System.Diagnostics;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;

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
        var userId = command.UserId;

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);

        if (calendar is null)
        {
            return new Result<CalendarItem>.NotFound();
        }

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

        // The timing must match the item's own case: an event is rescheduled with a new span, a
        // task with a new due date (its assignee and source are kept).
        switch (item.Schedule, command.Timing)
        {
            case (ItemSchedule.Event current, ItemTiming.Event timing):
                if (Period.TryCreate(timing.StartsAt, timing.EndsAt, timing.IsAllDay) is not PeriodValidationResult.Valid(var period))
                {
                    return new Result<CalendarItem>.Validation(ValidationProblem.Of(Period.TryCreate(timing.StartsAt, timing.EndsAt, timing.IsAllDay) switch
                    {
                        PeriodValidationResult.Invalid(var message) => message,
                        PeriodValidationResult.Valid => throw new UnreachableException("Already excluded by the enclosing check."),
                    }));
                }

                await items.AppendAsync(command.ItemId, [new EventRescheduled(command.ItemId, current.Period, period, userId, now)], cancellationToken);

                return new Result<CalendarItem>.Success(item with { Schedule = new ItemSchedule.Event(period), LastModifiedBy = userId });

            case (ItemSchedule.Task current, ItemTiming.Task timing):
                await items.AppendAsync(command.ItemId, [new TaskRescheduled(command.ItemId, current.DueDate, timing.DueDate, userId, now)], cancellationToken);

                return new Result<CalendarItem>.Success(item with { Schedule = current with { DueDate = timing.DueDate }, LastModifiedBy = userId });

            case (ItemSchedule.Event, ItemTiming.Task):
                return new Result<CalendarItem>.Validation(ValidationProblem.Of("An event requires both a start and an end time."));

            default:
                return new Result<CalendarItem>.Validation(ValidationProblem.Of("A task requires a due date."));
        }
    }
}

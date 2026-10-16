using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;

using FluentValidation;

namespace buddy.Features.Calendars;

// Replaces a calendar item's alarms (docs/backend/analysis/calendar-item-alarms.md). Same tier and
// no-op rule as UpdateItemRecurrence: re-sending the stored alarms appends nothing.
public static class UpdateItemAlarmsHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        UpdateItemAlarms command,
        IValidator<UpdateItemAlarms> validator,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<CalendarItem>.Validation(problem);
        }

        var userId = command.UserId;

        var calendar = Calendar.Rehydrate(await calendars.ReadAsync(command.CalendarId, cancellationToken));

        if (calendar is null)
        {
            return new Result<CalendarItem>.NotFound();
        }

        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<CalendarItem>();
        }

        var item = CalendarItem.Rehydrate(await items.ReadAsync(command.ItemId, cancellationToken));

        if (item is null || item.IsDeleted || item.CalendarId != command.CalendarId)
        {
            return new Result<CalendarItem>.NotFound();
        }

        if (item.Alarms.Equals(command.Alarms))
        {
            return new Result<CalendarItem>.Success(item);
        }

        await items.AppendAsync(
            command.ItemId,
            [new ItemAlarmsUpdated(command.ItemId, item.Alarms, command.Alarms, userId, time.GetUtcNow())],
            cancellationToken);

        return new Result<CalendarItem>.Success(item with { Alarms = command.Alarms, LastModifiedBy = userId });
    }
}

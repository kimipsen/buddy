using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;

using FluentValidation;

namespace buddy.Features.Calendars;

public static class UpdateItemRecurrenceHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        UpdateItemRecurrence command,
        IValidator<UpdateItemRecurrence> validator,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<CalendarItem>.Validation(problem);
        }

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

        // Re-sending the stored recurrence is a no-op, even for an item a reschedule has since
        // moved past its Until.
        if (item.Recurrence.Equals(command.Recurrence))
        {
            return new Result<CalendarItem>.Success(item);
        }

        var problems = RecurrenceRules.Problems(command.Recurrence, item.SeedDate).ToArray();

        if (problems.Length > 0)
        {
            return new Result<CalendarItem>.Validation(new ValidationProblem(
                problems.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Select(p => p.Message).ToArray())));
        }

        await items.AppendAsync(
            command.ItemId,
            [new RecurrenceUpdated(command.ItemId, item.Recurrence, command.Recurrence, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<CalendarItem>.Success(item with { Recurrence = command.Recurrence, LastModifiedBy = userId });
    }
}

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;

namespace buddy.Features.Calendars;

public static class SetOccurrenceSkipHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        SetOccurrenceSkip command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var calendar = Calendar.Rehydrate(await calendars.ReadAsync(command.CalendarId, cancellationToken));

        if (calendar is null)
        {
            return new Result<CalendarItem>.NotFound();
        }

        // Contribute, the tier of every other item edit. Unlike SetTaskCompletion there is no
        // Viewer exception for a task assigned to the caller: a skip changes what everyone on the
        // calendar sees, so a child can't skip their own chores.
        var access = await CalendarAuthorization.CheckContribute(calendar, command.UserId, groups, cancellationToken);

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

        // Skipping needs a repeating item and a date its rule actually produces, so a stale client
        // can't store a skip for a day the item doesn't run. Restoring needs neither: a skip
        // orphaned by a later rule change -- including the item becoming one-off, which would
        // otherwise hide its only occurrence for good -- must stay clearable. The same asymmetry as
        // ticking a packing-list item.
        if (command.IsSkipped)
        {
            if (item.Recurrence is not Recurrence.Repeating)
            {
                return new Result<CalendarItem>.Validation(ValidationProblem.Of("Only a repeating item can skip an occurrence; delete a one-off item instead."));
            }

            if (RecurrenceExpansion.ExpandDates(item.SeedDate, item.Recurrence, command.OccurrenceDate, command.OccurrenceDate).Count == 0)
            {
                return new Result<CalendarItem>.Validation(ValidationProblem.Of("The item has no occurrence on that date."));
            }
        }

        var before = item.SkippedDates.Contains(command.OccurrenceDate);

        if (before == command.IsSkipped)
        {
            return new Result<CalendarItem>.Success(item);
        }

        var skipChanged = new OccurrenceSkipChanged(command.ItemId, command.OccurrenceDate, before, command.IsSkipped, command.UserId, time.GetUtcNow());

        await items.AppendAsync(command.ItemId, [skipChanged], cancellationToken);

        return new Result<CalendarItem>.Success(CalendarItem.Replay([.. itemEvents, skipChanged]));
    }
}

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Calendars;

public static class SetTaskCompletionHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        SetTaskCompletion command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<CalendarItem>.NotFound();
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, guardians, cancellationToken);

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

        if (item.Kind != CalendarItemKind.Task)
        {
            return new Result<CalendarItem>.Validation("Only a task can be marked complete.");
        }

        var before = item.CompletionLog.GetValueOrDefault(command.OccurrenceDate, false);

        if (before == command.IsCompleted)
        {
            return new Result<CalendarItem>.Success(item);
        }

        var completionChanged = new TaskCompletionChanged(command.ItemId, command.OccurrenceDate, before, command.IsCompleted, userId, DateTimeOffset.UtcNow);

        await items.AppendAsync(command.ItemId, [completionChanged], cancellationToken);

        return new Result<CalendarItem>.Success(CalendarItem.Rehydrate([.. itemEvents, completionChanged])!);
    }
}

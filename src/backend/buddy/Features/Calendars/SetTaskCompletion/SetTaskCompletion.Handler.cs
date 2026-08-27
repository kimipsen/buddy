using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Progress;

using Wolverine;

namespace buddy.Features.Calendars;

public static class SetTaskCompletionHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        SetTaskCompletion command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<CalendarItem>.NotFound();
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, guardians, cancellationToken);

        // NotFound (no resolved role at all) is still denied outright -- only a Forbidden (a
        // resolved role below Contributor, e.g. the Viewer tier a child gets by default under
        // CreateGroupHandler.DefaultCalendarPolicy) gets the chance to fall through to the
        // self-completion check below.
        if (access == CalendarAccess.NotFound)
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

        // A Viewer can still toggle completion on a task assigned specifically to them: marking
        // your own chore done is narrower than the general "create/edit any item" contributor
        // right, so it shouldn't require the group to grant that just for this.
        var isSelfCompletingOwnTask = item.AssignedTo == userId;

        if (access != CalendarAccess.Allowed && !isSelfCompletingOwnTask)
        {
            return access.ToDeniedResult<CalendarItem>();
        }

        if (command.IsCompleted)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar!.TimeZoneId.Value);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date);

            if (command.OccurrenceDate > today)
            {
                return new Result<CalendarItem>.Validation("Cannot mark a future occurrence as complete.");
            }
        }

        var before = item.CompletionLog.GetValueOrDefault(command.OccurrenceDate, false);

        if (before == command.IsCompleted)
        {
            return new Result<CalendarItem>.Success(item);
        }

        var completionChanged = new TaskCompletionChanged(command.ItemId, command.OccurrenceDate, before, command.IsCompleted, userId, DateTimeOffset.UtcNow);

        await items.AppendAsync(command.ItemId, [completionChanged], cancellationToken);

        // Explicit cross-feature call, not a transaction with the append above -- see
        // docs/backend/analysis/gamified-progress.md. The task completion itself has already
        // succeeded by this point; a failure here just leaves the child's star count stale until
        // the next successful, idempotent completion change catches it up.
        if (item.AssignedTo is { } childId)
        {
            try
            {
                await bus.InvokeAsync(new RecordStarChange(childId, command.ItemId, command.OccurrenceDate, command.IsCompleted), cancellationToken);
            }
            catch
            {
                // Deliberately swallowed -- see the comment above.
            }
        }

        return new Result<CalendarItem>.Success(CalendarItem.Rehydrate([.. itemEvents, completionChanged])!);
    }
}

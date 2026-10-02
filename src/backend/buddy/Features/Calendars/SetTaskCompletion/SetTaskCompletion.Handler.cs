using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Progress;
using buddy.Features.TaskLibrary;

using Wolverine;

namespace buddy.Features.Calendars;

public static class SetTaskCompletionHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        SetTaskCompletion command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        ITaskTemplateEventStore templates,
        IGroupEventStore groups,
        IMessageBus bus,
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

        if (item.Schedule is not ItemSchedule.Task task)
        {
            return new Result<CalendarItem>.Validation(ValidationProblem.Of("Only a task can be marked complete."));
        }

        if (await ValidateSubtaskAsync(task, command, templates, cancellationToken) is { } subtaskError)
        {
            return subtaskError;
        }

        // A Viewer can still toggle completion on a task assigned specifically to them: marking
        // your own chore done is narrower than the general "create/edit any item" contributor
        // right, so it shouldn't require the group to grant that just for this.
        var isSelfCompletingOwnTask = task.AssignedTo == userId;

        if (access != CalendarAccess.Allowed && !isSelfCompletingOwnTask)
        {
            return access.ToDeniedResult<CalendarItem>();
        }

        if (ValidateNotFuture(command, calendar) is { } futureError)
        {
            return futureError;
        }

        var before = item.CompletionLog.Contains(new CompletionKey(command.OccurrenceDate, command.Target));

        if (before == command.IsCompleted)
        {
            return new Result<CalendarItem>.Success(item);
        }

        var completionChanged = new TaskCompletionChanged(command.ItemId, command.OccurrenceDate, before, command.IsCompleted, userId, DateTimeOffset.UtcNow, command.Target);

        await items.AppendAsync(command.ItemId, [completionChanged], cancellationToken);

        await TryRecordStarChangeAsync(task, command, bus, cancellationToken);

        return new Result<CalendarItem>.Success(CalendarItem.Replay([.. itemEvents, completionChanged]));
    }

    // The two completion modes never mix: a template-scheduled task completes per subtask (its
    // occurrences complete independently -- see CalendarOccurrenceExpansion), a plain task as a
    // whole. Also rejects a stale subtask id from an out-of-date client (a subtask removed, or the
    // whole template hard-deleted, since the template's last fetch) rather than silently writing a
    // phantom completion entry for a subtask that no longer exists.
    private static async Task<Result<CalendarItem>?> ValidateSubtaskAsync(
        ItemSchedule.Task task, SetTaskCompletion command, ITaskTemplateEventStore templates, CancellationToken cancellationToken)
    {
        switch (task.Source, command.Target)
        {
            case (TaskSource.FromTemplate, CompletionTarget.WholeTask):
                return new Result<CalendarItem>.Validation(ValidationProblem.Of("A template-scheduled task is completed one subtask at a time."));

            case (TaskSource.Freeform, CompletionTarget.Subtask):
                return new Result<CalendarItem>.Validation(ValidationProblem.Of("Only a template-scheduled task has subtasks."));

            case (TaskSource.FromTemplate fromTemplate, CompletionTarget.Subtask subtask):
                var template = TaskTemplate.Rehydrate(await templates.ReadAsync(new TaskTemplateId(fromTemplate.TaskTemplateId), cancellationToken));

                if (template is null || !template.Subtasks.Any(s => s.Id.Value == subtask.SubtaskId))
                {
                    return new Result<CalendarItem>.NotFound();
                }

                return null;

            default:
                return null;
        }
    }

    // A future occurrence can't already have happened -- only checked when marking complete,
    // since un-completing one is always allowed regardless of date.
    private static Result<CalendarItem>? ValidateNotFuture(SetTaskCompletion command, Calendar calendar)
    {
        if (!command.IsCompleted)
        {
            return null;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId.Value);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date);

        if (command.OccurrenceDate > today)
        {
            return new Result<CalendarItem>.Validation(ValidationProblem.Of("Cannot mark a future occurrence as complete."));
        }

        return null;
    }

    // Explicit cross-feature call, not a transaction with the append above -- see
    // docs/backend/analysis/gamified-progress.md. The task completion itself has already
    // succeeded by this point; a failure here just leaves the child's star count stale until the
    // next successful, idempotent completion change catches it up.
    private static async Task TryRecordStarChangeAsync(ItemSchedule.Task task, SetTaskCompletion command, IMessageBus bus, CancellationToken cancellationToken)
    {
        if (task.AssignedTo is not { } childId)
        {
            return;
        }

        try
        {
            await bus.InvokeAsync(new RecordStarChange(childId, command.ItemId, command.OccurrenceDate, command.IsCompleted, command.Target), cancellationToken);
        }
        catch
        {
            // Deliberately swallowed -- see the comment above.
        }
    }
}

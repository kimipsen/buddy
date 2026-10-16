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
        ILogger<SetTaskCompletion> logger,
        TimeProvider time,
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

        var subtaskCheck = await ValidateSubtaskAsync(task, command, templates, cancellationToken);

        if (subtaskCheck.Error is { } subtaskError)
        {
            return subtaskError;
        }

        var template = subtaskCheck.Template;

        // A Viewer can still toggle completion on a task assigned specifically to them: marking
        // your own chore done is narrower than the general "create/edit any item" contributor
        // right, so it shouldn't require the group to grant that just for this.
        var isSelfCompletingOwnTask = task.AssignedTo == userId;

        if (access != CalendarAccess.Allowed && !isSelfCompletingOwnTask)
        {
            return access.ToDeniedResult<CalendarItem>();
        }

        if (ValidateNotFuture(command, calendar, template, time) is { } futureError)
        {
            return futureError;
        }

        // A skipped occurrence isn't on anyone's list, so it can't be ticked; un-ticking stays
        // allowed, like a packing-list item off its weekdays
        // (docs/backend/analysis/skip-occurrences.md, edge cases).
        if (command.IsCompleted && item.SkippedDates.Contains(command.OccurrenceDate))
        {
            return new Result<CalendarItem>.Validation(ValidationProblem.Of("This occurrence is skipped."));
        }

        var before = item.CompletionLog.Contains(new CompletionKey(command.OccurrenceDate, command.Target));

        if (before == command.IsCompleted)
        {
            return new Result<CalendarItem>.Success(item);
        }

        var completionChanged = new TaskCompletionChanged(command.ItemId, command.OccurrenceDate, before, command.IsCompleted, userId, time.GetUtcNow(), command.Target);

        await items.AppendAsync(command.ItemId, [completionChanged], cancellationToken);

        var updated = CalendarItem.Replay([.. itemEvents, completionChanged]);

        await TryRecordStarChangeAsync(task, command.ItemId, command.OccurrenceDate, StarChangeFor(command, template, updated), bus, logger, cancellationToken);

        return new Result<CalendarItem>.Success(updated);
    }

    // The two completion modes never mix: a template-scheduled task completes per subtask (its
    // occurrences complete independently -- see CalendarOccurrenceExpansion), a plain task as a
    // whole. Also rejects a stale subtask id from an out-of-date client (a subtask removed, or the
    // whole template hard-deleted, since the template's last fetch) rather than silently writing a
    // phantom completion entry for a subtask that no longer exists. Marking complete a packing-list
    // item whose weekdays exclude the date is refused too; un-ticking one stays allowed, so a tick
    // recorded before a guardian narrowed the weekdays can be cleared. Returns the loaded template
    // for a template-scheduled task, so the date and star checks below don't read it again.
    private static async Task<SubtaskCheck> ValidateSubtaskAsync(
        ItemSchedule.Task task, SetTaskCompletion command, ITaskTemplateEventStore templates, CancellationToken cancellationToken)
    {
        switch (task.Source, command.Target)
        {
            case (TaskSource.FromTemplate, CompletionTarget.WholeTask):
                return new SubtaskCheck(new Result<CalendarItem>.Validation(ValidationProblem.Of("A template-scheduled task is completed one subtask at a time.")), null);

            case (TaskSource.Freeform, CompletionTarget.Subtask):
                return new SubtaskCheck(new Result<CalendarItem>.Validation(ValidationProblem.Of("Only a template-scheduled task has subtasks.")), null);

            case (TaskSource.FromTemplate fromTemplate, CompletionTarget.Subtask target):
                var template = TaskTemplate.Rehydrate(await templates.ReadAsync(new TaskTemplateId(fromTemplate.TaskTemplateId), cancellationToken));

                if (template?.Subtasks.FirstOrDefault(s => s.Id.Value == target.SubtaskId) is not { } subtask)
                {
                    return new SubtaskCheck(new Result<CalendarItem>.NotFound(), null);
                }

                if (command.IsCompleted && !subtask.AppliesOn(command.OccurrenceDate))
                {
                    return new SubtaskCheck(new Result<CalendarItem>.Validation(ValidationProblem.Of("This item isn't on the list that day.")), template);
                }

                return new SubtaskCheck(null, template);

            default:
                return new SubtaskCheck(null, null);
        }
    }

    private sealed record SubtaskCheck(Result<CalendarItem>? Error, TaskTemplate? Template);

    // A future occurrence can't already have happened -- only checked when marking complete,
    // since un-completing one is always allowed regardless of date. A packing list is the
    // exception: it can be packed the evening before, so tomorrow's occurrence is allowed too
    // (docs/backend/analysis/packing-checklists.md, Question 4). When to *show* it is the client's
    // choice (ChecklistOptions.EveningBeforeFrom); the server only bounds it to one day ahead.
    private static Result<CalendarItem>? ValidateNotFuture(SetTaskCompletion command, Calendar calendar, TaskTemplate? template, TimeProvider time)
    {
        if (!command.IsCompleted)
        {
            return null;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId.Value);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).Date);

        var latest = template is { Kind: TaskTemplateKind.Checklist } ? today.AddDays(1) : today;

        if (command.OccurrenceDate > latest)
        {
            return new Result<CalendarItem>.Validation(ValidationProblem.Of("Cannot mark a future occurrence as complete."));
        }

        return null;
    }

    // Explicit cross-feature call, not a transaction with the append above -- see
    // docs/backend/analysis/gamified-progress.md. The task completion itself has already
    // succeeded by this point; a failure here just leaves the child's star count stale until the
    // next successful, idempotent completion change catches it up.
    private static async Task TryRecordStarChangeAsync(
        ItemSchedule.Task task, CalendarItemId itemId, DateOnly occurrenceDate, StarChange change, IMessageBus bus, ILogger logger, CancellationToken cancellationToken)
    {
        if (task.AssignedTo is not { } childId)
        {
            return;
        }

        try
        {
            await bus.InvokeAsync(new RecordStarChange(childId, itemId, occurrenceDate, change.IsCompleted, change.Target), cancellationToken);
        }
        catch (Exception exception)
        {
            // Deliberately swallowed -- see the comment above. Logged, so a stale star count can
            // be traced back to its cause; a cancelled request isn't a failure worth reporting.
            if (exception is not OperationCanceledException)
            {
                logger.StarChangeFailed(exception, childId.Value, itemId.Value);
            }
        }
    }

    private sealed record StarChange(bool IsCompleted, CompletionTarget Target);

    // Which star the completion change earns or revokes. A routine step or a plain task earns its
    // own star. A packing list earns one WholeTask star per occurrence, held while every applicable
    // item is ticked; a single item earns nothing (docs/backend/analysis/packing-checklists.md,
    // Question 5). Sent on every change rather than only when "all packed" flips: RecordStarChange
    // is idempotent, so a star change that failed earlier is caught up by the next tick.
    private static StarChange StarChangeFor(SetTaskCompletion command, TaskTemplate? template, CalendarItem updated)
    {
        if (template is not { Kind: TaskTemplateKind.Checklist })
        {
            return new StarChange(command.IsCompleted, command.Target);
        }

        var allPacked = template.Subtasks
            .Where(s => s.AppliesOn(command.OccurrenceDate))
            .All(s => updated.CompletionLog.Contains(new CompletionKey(command.OccurrenceDate, new CompletionTarget.Subtask(s.Id.Value))));

        return new StarChange(allPacked, new CompletionTarget.WholeTask());
    }
}

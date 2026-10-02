namespace buddy.Features.Calendars;

// What a completion (and the star it earns) is for: a whole plain task, or one subtask of a
// template-scheduled task. Replaces a Guid? SubtaskId where null meant "the whole task". Persisted
// inside TaskCompletionChanged and Progress's StarAwarded/StarRevoked through
// CompletionTargetJsonConverter. See docs/backend/analysis/eliminate-nulls.md, Phase 5.3.
public union CompletionTarget(CompletionTarget.WholeTask, CompletionTarget.Subtask)
{
    public sealed record WholeTask;

    // A raw Guid, not TaskLibrary's SubtaskId -- Calendars takes no compile dependency on TaskLibrary.
    public sealed record Subtask(Guid SubtaskId);
}

// One completed occurrence of a task: CalendarItem.CompletionLog holds only completed ones, so
// "not completed" is simply absence.
public sealed record CompletionKey(DateOnly OccurrenceDate, CompletionTarget Target);

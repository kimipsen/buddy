using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Progress;

public union ProgressEvent(
    ProgressStarted,
    StarAwarded,
    StarRevoked,
    MilestoneUnlocked
)
{
    public static ProgressEvent FromPayload(object payload) => payload switch
    {
        ProgressStarted e => e,
        StarAwarded e => e,
        StarRevoked e => e,
        MilestoneUnlocked e => e,
        _ => throw new ArgumentException($"Unknown progress event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        ProgressStarted => nameof(ProgressStarted),
        StarAwarded => nameof(StarAwarded),
        StarRevoked => nameof(StarRevoked),
        MilestoneUnlocked => nameof(MilestoneUnlocked),
    };
}

public sealed record ProgressStarted(ProgressId Id, UserId ChildId, DateTimeOffset OccurredAt);

// Mirrors TaskCompletionChanged's own occurrence keying (CalendarItemId + OccurrenceDate +
// SubtaskId) so a recurring task's daily instances -- and, for a template-scheduled task, each of
// its independently-completable subtasks -- are awarded independently, not once for the whole
// series/item. SubtaskId defaults to null (same backward-compatible technique as
// TaskCompletionChanged.SubtaskId) so existing persisted events, which predate per-subtask
// completion, still deserialize as a plain task's award.
public sealed record StarAwarded(ProgressId Id, CalendarItemId SourceItemId, DateOnly OccurrenceDate, DateTimeOffset OccurredAt, Guid? SubtaskId = null);

// Mirrors the child un-completing the same occurrence (TaskCompletionChanged After: false) --
// not a penalty event, the same correction semantics as DoseStatusChanged's After: Pending undo.
public sealed record StarRevoked(ProgressId Id, CalendarItemId SourceItemId, DateOnly OccurrenceDate, DateTimeOffset OccurredAt, Guid? SubtaskId = null);

public sealed record MilestoneUnlocked(ProgressId Id, int Threshold, DateTimeOffset OccurredAt);

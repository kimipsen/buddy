using buddy.Features.Users;

namespace buddy.Features.Feelings;

public union FeelingsJournalEvent(
    FeelingsJournalStarted,
    FeelingCheckedIn,
    FeelingCheckInEdited,
    FeelingCheckInRemoved,
    CheckInPromptTimesSet,
    FeelingsDisplayModeSet
)
{
    public static FeelingsJournalEvent FromPayload(object payload) => payload switch
    {
        FeelingsJournalStarted e => e,
        FeelingCheckedIn e => e,
        FeelingCheckInEdited e => e,
        FeelingCheckInRemoved e => e,
        CheckInPromptTimesSet e => e,
        FeelingsDisplayModeSet e => e,
        _ => throw new ArgumentException($"Unknown feelings journal event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        FeelingsJournalStarted => nameof(FeelingsJournalStarted),
        FeelingCheckedIn => nameof(FeelingCheckedIn),
        FeelingCheckInEdited => nameof(FeelingCheckInEdited),
        FeelingCheckInRemoved => nameof(FeelingCheckInRemoved),
        CheckInPromptTimesSet => nameof(CheckInPromptTimesSet),
        FeelingsDisplayModeSet => nameof(FeelingsDisplayModeSet),
    };
}

// Appended lazily, in the same CreateAsync as the first write for the child (a check-in, prompt
// times or the display mode) -- like SleepDiaryStarted.
public sealed record FeelingsJournalStarted(FeelingsJournalId Id, UserId ChildId, DateTimeOffset OccurredAt);

// CheckIn.LoggedBy == the child for a self-report, a guardian for an observation.
public sealed record FeelingCheckedIn(FeelingsJournalId Id, FeelingCheckIn CheckIn, DateTimeOffset OccurredAt);

// A full overwrite of a guardian-logged check-in: same id, LoggedBy and RecordedAt.
public sealed record FeelingCheckInEdited(FeelingsJournalId Id, FeelingCheckIn Before, FeelingCheckIn After, UserId ModifiedBy, DateTimeOffset OccurredAt);

// The child's undo, or a guardian removing a check-in; ModifiedBy says which.
public sealed record FeelingCheckInRemoved(FeelingsJournalId Id, FeelingCheckIn Before, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record CheckInPromptTimesSet(FeelingsJournalId Id, IReadOnlyList<TimeOnly> Before, IReadOnlyList<TimeOnly> After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record FeelingsDisplayModeSet(FeelingsJournalId Id, FeelingsDisplayMode Before, FeelingsDisplayMode After, UserId ModifiedBy, DateTimeOffset OccurredAt);

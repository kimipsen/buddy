using buddy.Features.Users;

namespace buddy.Features.FirstThen;

public union FirstThenEvent(
    FirstThenBoardStarted,
    FirstThenBoardSet,
    FirstThenFirstCompleted,
    FirstThenFirstReopened,
    FirstThenBoardEnded
)
{
    public static FirstThenEvent FromPayload(object payload) => payload switch
    {
        FirstThenBoardStarted e => e,
        FirstThenBoardSet e => e,
        FirstThenFirstCompleted e => e,
        FirstThenFirstReopened e => e,
        FirstThenBoardEnded e => e,
        _ => throw new ArgumentException($"Unknown First-Then event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        FirstThenBoardStarted => nameof(FirstThenBoardStarted),
        FirstThenBoardSet => nameof(FirstThenBoardSet),
        FirstThenFirstCompleted => nameof(FirstThenFirstCompleted),
        FirstThenFirstReopened => nameof(FirstThenFirstReopened),
        FirstThenBoardEnded => nameof(FirstThenBoardEnded),
    };
}

// Appended once, together with the first FirstThenBoardSet, as SleepDiaryStarted is.
public sealed record FirstThenBoardStarted(FirstThenBoardId Id, UserId ChildId, DateTimeOffset OccurredAt);

// A full overwrite: a new board replaces whatever was there and starts in Waiting.
public sealed record FirstThenBoardSet(
    FirstThenBoardId Id,
    int Revision,
    FirstThenCard First,
    FirstThenCard Then,
    int? ThenMinutes,
    UserId SetBy,
    DateTimeOffset OccurredAt);

public sealed record FirstThenFirstCompleted(FirstThenBoardId Id, int Revision, UserId CompletedBy, DateTimeOffset OccurredAt);

// The mis-tap undo: back to Waiting.
public sealed record FirstThenFirstReopened(FirstThenBoardId Id, int Revision, UserId ReopenedBy, DateTimeOffset OccurredAt);

// One event for both "we're finished" and "never mind"; EndedBy says who.
public sealed record FirstThenBoardEnded(FirstThenBoardId Id, int Revision, UserId EndedBy, DateTimeOffset OccurredAt);

using Marten.Events.Aggregation;

namespace buddy.Features.FirstThen;

// Thin wrapper: Marten can't use the sealed-record FirstThenBoardId as a document Id. See
// PickupScheduleSnapshot and docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record FirstThenBoardSnapshot(Guid Id, FirstThenBoard FirstThenBoard);

// Inline snapshot in the shared "snapshots" schema -- derived, rebuildable state, never a second
// source of truth.
public sealed class FirstThenBoardSnapshotProjection : SingleStreamProjection<FirstThenBoardSnapshot, Guid>
{
    public static FirstThenBoardSnapshot Create(FirstThenBoardStarted started) =>
        new(started.Id.Value, FirstThenBoard.Start(FirstThenEvent.FromPayload(started)));

    public FirstThenBoardSnapshot Apply(FirstThenBoardSnapshot current, FirstThenBoardSet set) =>
        current with { FirstThenBoard = FirstThenBoard.Advance(current.FirstThenBoard, FirstThenEvent.FromPayload(set)) };

    public FirstThenBoardSnapshot Apply(FirstThenBoardSnapshot current, FirstThenFirstCompleted completed) =>
        current with { FirstThenBoard = FirstThenBoard.Advance(current.FirstThenBoard, FirstThenEvent.FromPayload(completed)) };

    public FirstThenBoardSnapshot Apply(FirstThenBoardSnapshot current, FirstThenFirstReopened reopened) =>
        current with { FirstThenBoard = FirstThenBoard.Advance(current.FirstThenBoard, FirstThenEvent.FromPayload(reopened)) };

    public FirstThenBoardSnapshot Apply(FirstThenBoardSnapshot current, FirstThenBoardEnded ended) =>
        current with { FirstThenBoard = FirstThenBoard.Advance(current.FirstThenBoard, FirstThenEvent.FromPayload(ended)) };
}

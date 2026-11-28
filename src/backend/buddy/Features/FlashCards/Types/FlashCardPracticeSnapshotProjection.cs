using Marten.Events.Aggregation;

namespace buddy.Features.FlashCards;

public sealed record FlashCardPracticeSnapshot(Guid Id, FlashCardPractice FlashCardPractice);

// Inline snapshot of FlashCardPractice in the shared "snapshots" schema. Detailed per-card results
// stay in events; Advance retains only bounded compact weights, so the snapshot stays small.
public sealed class FlashCardPracticeSnapshotProjection : SingleStreamProjection<FlashCardPracticeSnapshot, Guid>
{
    public static FlashCardPracticeSnapshot Create(FlashCardPracticeStarted started) =>
        new(started.Id.Value, FlashCardPractice.Start(FlashCardPracticeEvent.FromPayload(started)));

    public FlashCardPracticeSnapshot Apply(FlashCardPracticeSnapshot current, FlashCardSetEnabled enabled) =>
        current with { FlashCardPractice = FlashCardPractice.Advance(current.FlashCardPractice, FlashCardPracticeEvent.FromPayload(enabled)) };

    public FlashCardPracticeSnapshot Apply(FlashCardPracticeSnapshot current, FlashCardSetDisabled disabled) =>
        current with { FlashCardPractice = FlashCardPractice.Advance(current.FlashCardPractice, FlashCardPracticeEvent.FromPayload(disabled)) };

    public FlashCardPracticeSnapshot Apply(FlashCardPracticeSnapshot current, FlashCardGameFinished finished) =>
        current with { FlashCardPractice = FlashCardPractice.Advance(current.FlashCardPractice, FlashCardPracticeEvent.FromPayload(finished)) };
}

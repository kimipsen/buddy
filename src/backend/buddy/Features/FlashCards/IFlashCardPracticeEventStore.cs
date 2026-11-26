namespace buddy.Features.FlashCards;

public interface IFlashCardPracticeEventStore
{
    Task<IReadOnlyCollection<FlashCardPracticeEvent>> ReadAsync(FlashCardPracticeId id, CancellationToken cancellationToken);

    // An untracked read of the inline snapshot: config and the recent games, without rehydrating a
    // stream that is never truncated.
    Task<FlashCardPractice?> FindSnapshotAsync(FlashCardPracticeId id, CancellationToken cancellationToken);

    // The stream's current version, null when the child has no practice stream. Makes the next
    // AppendAsync an expected-version append. Call it BEFORE FindSnapshotAsync (see
    // StreamVersionTracker.ObserveVersion).
    Task<long?> ReadVersionAsync(FlashCardPracticeId id, CancellationToken cancellationToken);

    // A finished game older than the snapshot's window, by a typed event query scoped to the child's
    // stream (never a stream read). An untracked read.
    Task<FlashCardGameFinished?> FindFinishedGameAsync(FlashCardPracticeId id, Guid gameId, CancellationToken cancellationToken);

    // A lost race on the id surfaces as JasperFx.ConcurrencyException.
    Task<IReadOnlyCollection<FlashCardPracticeEvent>> CreateAsync(FlashCardPracticeId id, IReadOnlyCollection<FlashCardPracticeEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(FlashCardPracticeId id, IReadOnlyCollection<FlashCardPracticeEvent> events, CancellationToken cancellationToken);
}

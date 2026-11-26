using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public interface IFlashCardSetEventStore
{
    Task<IReadOnlyCollection<FlashCardSetEvent>> ReadAsync(FlashCardSetId id, CancellationToken cancellationToken);

    Task<FlashCardSet?> FindSnapshotAsync(FlashCardSetId id, CancellationToken cancellationToken);

    // Starts the stream and writes the owner's index row in the same transaction. A lost race on the
    // id surfaces as JasperFx.ConcurrencyException.
    Task<IReadOnlyCollection<FlashCardSetEvent>> CreateAsync(FlashCardSetId id, IReadOnlyCollection<FlashCardSetEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(FlashCardSetId id, IReadOnlyCollection<FlashCardSetEvent> events, CancellationToken cancellationToken);

    // Every set (deleted ones included) created by one of the owners.
    Task<IReadOnlyCollection<FlashCardSetId>> ListIdsByOwnersAsync(IReadOnlyCollection<UserId> owners, CancellationToken cancellationToken);
}

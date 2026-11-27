using buddy.Common.Concurrency;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.FlashCards;

public sealed class MartenFlashCardSetEventStore(IFlashCardsStore store, IBuiltInCatalogue catalogue) : IFlashCardSetEventStore
{
    public async Task<IReadOnlyCollection<FlashCardSetEvent>> ReadAsync(FlashCardSetId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => FlashCardSetEvent.FromPayload(e.Data))];
    }

    public async Task<FlashCardSet?> FindSnapshotAsync(FlashCardSetId id, CancellationToken cancellationToken)
    {
        // A catalogue id is synthesised, never stored.
        if (catalogue.TryFind(id) is { } builtIn)
        {
            return builtIn;
        }

        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<FlashCardSetSnapshot>(id.Value, cancellationToken);

        return snapshot?.FlashCardSet;
    }

    public async Task<IReadOnlyCollection<FlashCardSetEvent>> CreateAsync(FlashCardSetId id, IReadOnlyCollection<FlashCardSetEvent> events, CancellationToken cancellationToken)
    {
        var ownerId = events.FirstOrDefault() switch
        {
            FlashCardSetCreated created => created.OwnerId,
            _ => throw new InvalidOperationException("The first event of a new flash card set stream must be FlashCardSetCreated."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty flash card set event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, payloads);
        session.Store(new FlashCardSetIndexDocument(id.Value, ownerId.Value));

        await session.SaveStartedStreamAsync("flash card set", cancellationToken);

        return events;
    }

    public async Task AppendAsync(FlashCardSetId id, IReadOnlyCollection<FlashCardSetEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty flash card set event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<FlashCardSetId>> ListIdsByOwnersAsync(IReadOnlyCollection<UserId> owners, CancellationToken cancellationToken)
    {
        if (owners.Count == 0)
        {
            return [];
        }

        var ownerIds = owners.Select(o => o.Value).ToArray();

        await using var session = store.QuerySession();
        var documents = await session.Query<FlashCardSetIndexDocument>()
            .Where(d => ownerIds.Contains(d.OwnerId))
            .ToListAsync(cancellationToken);

        return [.. documents.Select(d => new FlashCardSetId(d.Id))];
    }
}

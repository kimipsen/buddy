using buddy.Common.Concurrency;

using Marten;

namespace buddy.Features.FlashCards;

public sealed class MartenFlashCardPracticeEventStore(IFlashCardsStore store) : IFlashCardPracticeEventStore
{
    public async Task<IReadOnlyCollection<FlashCardPracticeEvent>> ReadAsync(FlashCardPracticeId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => FlashCardPracticeEvent.FromPayload(e.Data))];
    }

    public async Task<FlashCardPractice?> FindSnapshotAsync(FlashCardPracticeId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<FlashCardPracticeSnapshot>(id.Value, cancellationToken);

        return snapshot?.FlashCardPractice;
    }

    public async Task<long?> ReadVersionAsync(FlashCardPracticeId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var state = await session.Events.FetchStreamStateAsync(id.Value, cancellationToken);

        if (state is null)
        {
            return null;
        }

        session.ObserveVersion(id.Value, state.Version);

        return state.Version;
    }

    public async Task<IReadOnlyCollection<FlashCardPracticeEvent>> CreateAsync(FlashCardPracticeId id, IReadOnlyCollection<FlashCardPracticeEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not FlashCardPracticeStarted)
        {
            throw new InvalidOperationException("The first event of a new flash card practice stream must be FlashCardPracticeStarted.");
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty flash card practice event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, payloads);

        await session.SaveStartedStreamAsync("flash card practice", cancellationToken);

        return events;
    }

    public async Task AppendAsync(FlashCardPracticeId id, IReadOnlyCollection<FlashCardPracticeEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty flash card practice event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }
}

using buddy.Common.Concurrency;

namespace buddy.Features.SleepDiaries;

public sealed class MartenSleepDiaryEventStore(ISleepDiariesStore store) : ISleepDiaryEventStore
{
    public async Task<IReadOnlyCollection<SleepDiaryEvent>> ReadAsync(SleepDiaryId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => SleepDiaryEvent.FromPayload(e.Data))];
    }

    public async Task<SleepDiary?> FindSnapshotAsync(SleepDiaryId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<SleepDiarySnapshot>(id.Value, cancellationToken);

        return snapshot?.SleepDiary;
    }

    public async Task CreateAsync(SleepDiaryId id, IReadOnlyCollection<SleepDiaryEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not SleepDiaryStarted)
        {
            throw new InvalidOperationException("The first event of a new sleep diary stream must be SleepDiaryStarted.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(SleepDiaryId id, IReadOnlyCollection<SleepDiaryEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    private static object[] ToPayloads(IReadOnlyCollection<SleepDiaryEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty sleep diary event."))];
}

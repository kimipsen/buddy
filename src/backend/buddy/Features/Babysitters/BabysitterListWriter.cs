using buddy.Features.Users;

namespace buddy.Features.Babysitters;

// The read-decide-append steps every write slice shares, same as WorkLocationScheduleWriter: the
// stream is created lazily by the first write, and loading through ReadAsync keeps the append
// expected-version (StreamVersionTracker).
internal sealed record LoadedBabysitterList(BabysitterList List, bool Exists);

internal static class BabysitterListWriter
{
    public static async Task<LoadedBabysitterList> LoadAsync(IBabysitterListEventStore store, UserId guardianId, CancellationToken cancellationToken)
    {
        var events = await store.ReadAsync(BabysitterListId.ForGuardian(guardianId), cancellationToken);

        return BabysitterList.Rehydrate(events) is { } list
            ? new LoadedBabysitterList(list, Exists: true)
            : new LoadedBabysitterList(BabysitterList.Empty(guardianId), Exists: false);
    }

    // Persists newEvents (no-op when empty) and returns the list with them applied.
    public static async Task<BabysitterList> SaveAsync(
        IBabysitterListEventStore store,
        LoadedBabysitterList loaded,
        IReadOnlyList<BabysitterEvent> newEvents,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (newEvents.Count == 0)
        {
            return loaded.List;
        }

        var list = loaded.List;

        if (loaded.Exists)
        {
            await store.AppendAsync(list.Id, newEvents, cancellationToken);
        }
        else
        {
            await store.CreateAsync(list.Id, [new BabysitterListStarted(list.Id, list.GuardianId, now), .. newEvents], cancellationToken);
        }

        return newEvents.Aggregate(list, (current, e) => BabysitterList.Advance(current, e));
    }
}

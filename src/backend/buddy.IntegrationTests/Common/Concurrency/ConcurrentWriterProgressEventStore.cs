using System.Collections.Concurrent;

using buddy.Common.Concurrency;
using buddy.Features.Progress;

namespace buddy.IntegrationTests.Common.Concurrency;

// Test-only IProgressEventStore decorator (registered once by BuddyApiFixture), modelled on
// ConcurrentWriterUserEventStore: it simulates a second request landing between a handler's
// ReadAsync and its append, which real parallel requests can't hit deterministically. Inert unless a
// test arms a child with InterleaveNextReads; it then fires on the first `times` reads of that
// child's stream inside a handler's tracking scope (reads outside one don't consume it). The
// concurrent events go straight through Marten so the tracker never sees them: into a new stream
// (with a leading ProgressStarted supplied by the factory) when the read found none, else appended.
public sealed class ConcurrentWriterProgressEventStore(MartenProgressEventStore inner, IProgressStore store) : IProgressEventStore
{
    private sealed class Arming(int remaining, Func<int, ProgressEvent[]> factory)
    {
        public int Remaining = remaining;
        public int Fired;
        public Func<int, ProgressEvent[]> Factory { get; } = factory;
    }

    private static readonly ConcurrentDictionary<Guid, Arming> Armed = new();

    // `concurrentEvents` receives the 0-based firing number and returns the events to write.
    public static void InterleaveNextReads(Guid childId, int times, Func<int, ProgressEvent[]> concurrentEvents) =>
        Armed[childId] = new Arming(times, concurrentEvents);

    public static void Disarm(Guid childId)
    {
        Armed.TryRemove(childId, out _);
        Lookups.TryRemove(childId, out _);
    }

    // Counts FindFlashCardAwardAsync calls for a child, to assert a request used the scoped event
    // query (and not a stream read) for the GameId lookup.
    private static readonly ConcurrentDictionary<Guid, int> Lookups = new();

    public static void RecordLookups(Guid childId) => Lookups[childId] = 0;

    public static int LookupCount(Guid childId) => Lookups.GetValueOrDefault(childId);

    public async Task<IReadOnlyCollection<ProgressEvent>> ReadAsync(ProgressId id, CancellationToken cancellationToken)
    {
        var events = await inner.ReadAsync(id, cancellationToken);

        if (StreamVersionTracker.IsTracking && Armed.TryGetValue(id.Value, out var arming))
        {
            var fire = false;
            var number = 0;

            lock (arming)
            {
                if (arming.Remaining > 0)
                {
                    arming.Remaining--;
                    number = arming.Fired++;
                    fire = true;
                }
            }

            if (fire)
            {
                var payloads = arming.Factory(number).Select(e => e.Value!).ToArray();
                await using var session = store.LightweightSession();

                if (events.Count == 0)
                {
                    session.Events.StartStream(id.Value, payloads);
                }
                else
                {
                    session.Events.Append(id.Value, payloads);
                }

                await session.SaveChangesAsync(cancellationToken);
            }
        }

        return events;
    }

    public Task<ChildProgress?> FindSnapshotAsync(ProgressId id, CancellationToken cancellationToken) =>
        inner.FindSnapshotAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<ProgressEvent>> CreateAsync(ProgressId id, IReadOnlyCollection<ProgressEvent> events, CancellationToken cancellationToken) =>
        inner.CreateAsync(id, events, cancellationToken);

    public Task<FlashCardStarsAwarded?> FindFlashCardAwardAsync(ProgressId id, Guid gameId, CancellationToken cancellationToken)
    {
        if (Lookups.ContainsKey(id.Value))
        {
            Lookups.AddOrUpdate(id.Value, 1, (_, count) => count + 1);
        }

        return inner.FindFlashCardAwardAsync(id, gameId, cancellationToken);
    }

    public Task AppendAsync(ProgressId id, IReadOnlyCollection<ProgressEvent> events, CancellationToken cancellationToken) =>
        inner.AppendAsync(id, events, cancellationToken);
}

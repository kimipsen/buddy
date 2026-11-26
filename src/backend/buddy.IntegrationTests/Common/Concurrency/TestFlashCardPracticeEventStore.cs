using System.Collections.Concurrent;

using buddy.Common.Concurrency;
using buddy.Features.FlashCards;

namespace buddy.IntegrationTests.Common.Concurrency;

// Test-only IFlashCardPracticeEventStore decorator (registered once by BuddyApiFixture), inert
// until a test arms a child -- modelled on ConcurrentWriterProgressEventStore. Three seams, all
// armed per child so parallel tests never trigger each other, and all cleared by Disarm:
//   - FailNextFinishedAppends: throws on the next appends of a FlashCardGameFinished, i.e. AFTER the
//     progress write (the award is committed before finish appends the history entry).
//   - OnNextSnapshotRead: runs an action right after the handler's version read, when it reads the
//     snapshot (the read-order seam); the action writes through Marten so the tracker never sees it.
//   - GateNextFinishedAppend: parks the append on a gate until the test releases it (hang-safe:
//     every wait is bounded, and Disarm releases the gate).
// Every call is logged per child, so a test can assert what a request did NOT call.
public sealed class TestFlashCardPracticeEventStore(MartenFlashCardPracticeEventStore inner, IFlashCardsStore store) : IFlashCardPracticeEventStore
{
    public sealed class Gate
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        public Task Reached => _reached.Task.WaitAsync(Timeout);

        public void Release() => _release.TrySetResult();

        internal async Task WaitAsync()
        {
            _reached.TrySetResult();
            await _release.Task.WaitAsync(Timeout);
        }
    }

    private sealed class Arming
    {
        public int FailAppends;
        public Exception? FailWith;
        public Func<Task>? OnSnapshotRead;
        public Gate? GateOnAppend;
        public List<string> Calls { get; } = [];
    }

    private static readonly ConcurrentDictionary<Guid, Arming> Armed = new();

    public static void FailNextFinishedAppends(Guid childId, int times, Exception exception)
    {
        var arming = Armed.GetOrAdd(childId, _ => new Arming());
        arming.FailAppends = times;
        arming.FailWith = exception;
    }

    public static void OnNextSnapshotRead(Guid childId, Func<Task> action) =>
        Armed.GetOrAdd(childId, _ => new Arming()).OnSnapshotRead = action;

    public static Gate GateNextFinishedAppend(Guid childId)
    {
        var gate = new Gate();
        Armed.GetOrAdd(childId, _ => new Arming()).GateOnAppend = gate;

        return gate;
    }

    // Starts recording calls for the child without changing any behaviour.
    public static void Record(Guid childId) => Armed.GetOrAdd(childId, _ => new Arming());

    public static IReadOnlyList<string> Calls(Guid childId) =>
        Armed.TryGetValue(childId, out var arming) ? [.. arming.Calls] : [];

    public static void Disarm(Guid childId)
    {
        if (Armed.TryRemove(childId, out var arming))
        {
            arming.GateOnAppend?.Release();
        }
    }

    private static void Log(FlashCardPracticeId id, string call)
    {
        if (Armed.TryGetValue(id.Value, out var arming))
        {
            lock (arming.Calls)
            {
                arming.Calls.Add(call);
            }
        }
    }

    public Task<IReadOnlyCollection<FlashCardPracticeEvent>> ReadAsync(FlashCardPracticeId id, CancellationToken cancellationToken)
    {
        Log(id, nameof(ReadAsync));

        return inner.ReadAsync(id, cancellationToken);
    }

    public async Task<FlashCardPractice?> FindSnapshotAsync(FlashCardPracticeId id, CancellationToken cancellationToken)
    {
        Log(id, nameof(FindSnapshotAsync));

        if (StreamVersionTracker.IsTracking && Armed.TryGetValue(id.Value, out var arming))
        {
            Func<Task>? action;

            lock (arming)
            {
                action = arming.OnSnapshotRead;
                arming.OnSnapshotRead = null;
            }

            if (action is not null)
            {
                await action();
            }
        }

        return await inner.FindSnapshotAsync(id, cancellationToken);
    }

    public Task<FlashCardGameFinished?> FindFinishedGameAsync(FlashCardPracticeId id, Guid gameId, CancellationToken cancellationToken)
    {
        Log(id, nameof(FindFinishedGameAsync));

        return inner.FindFinishedGameAsync(id, gameId, cancellationToken);
    }

    public Task<long?> ReadVersionAsync(FlashCardPracticeId id, CancellationToken cancellationToken)
    {
        Log(id, nameof(ReadVersionAsync));

        return inner.ReadVersionAsync(id, cancellationToken);
    }

    public Task<IReadOnlyCollection<FlashCardPracticeEvent>> CreateAsync(FlashCardPracticeId id, IReadOnlyCollection<FlashCardPracticeEvent> events, CancellationToken cancellationToken)
    {
        Log(id, nameof(CreateAsync));

        return inner.CreateAsync(id, events, cancellationToken);
    }

    public async Task AppendAsync(FlashCardPracticeId id, IReadOnlyCollection<FlashCardPracticeEvent> events, CancellationToken cancellationToken)
    {
        Log(id, nameof(AppendAsync));

        if (events.Any(e => e.Value is FlashCardGameFinished) && Armed.TryGetValue(id.Value, out var arming))
        {
            Gate? gate;
            Exception? failure = null;

            lock (arming)
            {
                gate = arming.GateOnAppend;
                arming.GateOnAppend = null;

                if (arming.FailAppends > 0)
                {
                    arming.FailAppends--;
                    failure = arming.FailWith;
                }
            }

            if (gate is not null)
            {
                await gate.WaitAsync();
            }

            if (failure is not null)
            {
                throw failure;
            }
        }

        await inner.AppendAsync(id, events, cancellationToken);
    }

    // Appends events behind the tracker's back, like a concurrent request would.
    public async Task AppendBehindTheTrackerAsync(FlashCardPracticeId id, params FlashCardPracticeEvent[] events)
    {
        await using var session = store.LightweightSession();
        session.Events.Append(id.Value, events.Select(e => e.Value!).ToArray());
        await session.SaveChangesAsync();
    }
}

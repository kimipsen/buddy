using System.Collections.Concurrent;

using JasperFx.Events;

using Marten;

namespace buddy.Common.Concurrency;

// Optimistic concurrency for every read-modify-append path, without threading a version through
// each handler. Every command handler rehydrates through its store's ReadAsync and later appends
// through the same store's AppendAsync; the tracker remembers, per handler invocation, the stream
// version each ReadAsync saw and turns the matching append into an expected-version append. If
// another request appended to that stream in between, Marten throws a ConcurrencyException at
// SaveChangesAsync, which ConcurrencyConflictMiddleware renders as 409 `concurrency_conflict`.
//
// Scope: StreamVersionScopeMiddleware opens one per Wolverine handler invocation (a nested
// IMessageBus.InvokeAsync shares its caller's scope, so a nested append advances the version the
// caller appends against instead of tripping a false conflict). With no scope active (store
// calls from tests or startup code) and for a stream that was not read in this scope, or did not
// exist yet when read, the append stays a plain unconditional append -- the old behavior.
//
// Keyed by (events schema, stream id): each feature has its own Marten store and schema, and a
// few streams share a Guid across schemas (e.g. a child's user stream and its progress stream).
public static class StreamVersionTracker
{
    private static readonly AsyncLocal<ConcurrentDictionary<(string Schema, Guid StreamId), long>?> Current = new();

    public static bool IsTracking => Current.Value is not null;

    // Returns null when a scope is already active: the outer owner closes it.
    public static IDisposable? BeginScope()
    {
        if (Current.Value is not null)
        {
            return null;
        }

        Current.Value = new();
        return new Scope();
    }

    // Call right after FetchStreamAsync with the raw stream it returned.
    public static void ObserveStream(this IQuerySession session, Guid streamId, IReadOnlyList<IEvent> events)
    {
        if (Current.Value is { } tracked)
        {
            tracked[Key(session, streamId)] = events.Count == 0 ? 0 : events[^1].Version;
        }
    }

    // Drop-in for session.Events.StartStream: a later append in the same scope expects the
    // version this start produces.
    public static void StartTrackedStream(this IDocumentSession session, Guid streamId, object[] payloads)
    {
        session.Events.StartStream(streamId, payloads);

        if (Current.Value is { } tracked)
        {
            tracked[Key(session, streamId)] = payloads.Length;
        }
    }

    // Drop-in for session.Events.Append: an expected-version append when this scope read (or
    // started, or already appended to) the stream, a plain append otherwise.
    public static void AppendTracked(this IDocumentSession session, Guid streamId, object[] payloads)
    {
        var key = Key(session, streamId);

        if (Current.Value is { } tracked && tracked.TryGetValue(key, out var version) && version > 0)
        {
            var expectedAfterAppend = version + payloads.Length;
            session.Events.Append(streamId, expectedAfterAppend, payloads);
            tracked[key] = expectedAfterAppend;
            return;
        }

        session.Events.Append(streamId, payloads);
    }

    private static (string Schema, Guid StreamId) Key(IQuerySession session, Guid streamId) =>
        (session.DocumentStore.Options.Events.DatabaseSchemaName, streamId);

    private sealed class Scope : IDisposable
    {
        public void Dispose() => Current.Value = null;
    }
}

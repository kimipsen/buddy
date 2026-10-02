namespace buddy.Common.Aggregates;

// Folds an event stream through an aggregate's Start (first event -> new state) and Advance
// (state + later event -> new state). Both are non-null, so an aggregate is never "null until its
// creation event" -- the only null is an empty stream, from Rehydrate. Replay is for a stream the
// caller has just created or otherwise knows is non-empty. See
// docs/backend/analysis/eliminate-nulls.md, Phase 2.
public static class EventReplay
{
    public static TState? Rehydrate<TEvent, TState>(
        IEnumerable<TEvent> events,
        Func<TEvent, TState> start,
        Func<TState, TEvent, TState> advance)
        where TState : class
    {
        using var enumerator = events.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            return null;
        }

        var state = start(enumerator.Current);

        while (enumerator.MoveNext())
        {
            state = advance(state, enumerator.Current);
        }

        return state;
    }

    public static TState Replay<TEvent, TState>(
        IEnumerable<TEvent> events,
        Func<TEvent, TState> start,
        Func<TState, TEvent, TState> advance)
        where TState : class =>
        Rehydrate(events, start, advance)
        ?? throw new InvalidOperationException($"Cannot replay an empty {typeof(TState).Name} event stream.");

    public static InvalidOperationException NotAStartEvent(string aggregate, string eventType) =>
        new($"A {aggregate} stream must start with its creation event, not {eventType}.");

    public static InvalidOperationException AlreadyStarted(string aggregate, string eventType) =>
        new($"{eventType} can only start a {aggregate} stream; this one has already started.");
}

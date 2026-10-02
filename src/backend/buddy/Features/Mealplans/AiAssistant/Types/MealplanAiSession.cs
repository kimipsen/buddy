using System.Collections.Immutable;
using buddy.Common.Aggregates;

namespace buddy.Features.Mealplans;

// Holds only decision-relevant state (the draft and its status) -- the conversation transcript
// itself is never needed to decide anything, so it's derived on demand from the raw event stream
// (see AiSessionHistoryBuilder) rather than carried here.
public sealed record MealplanAiSession(
    MealplanAiSessionId Id,
    DateOnly From,
    DateOnly To,
    ImmutableHashSet<MealSlot> RequestedSlots,
    ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealId> Draft,
    AiSessionStatus Status)
{
    public static MealplanAiSession? Rehydrate(IEnumerable<MealplanAiSessionEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static MealplanAiSession Replay(IEnumerable<MealplanAiSessionEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so
    // MealplanAiSessionSnapshotProjection can drive the same logic one Marten-delivered event at a
    // time instead of duplicating this switch. Deliberately not named Apply/Create -- those names
    // are a convention JasperFx's projection source generator scans for on any type used as a
    // projection document, and MealplanAiSession is that document (see Question 4/5 in
    // docs/backend/analysis/event-stream-snapshots.md).
    // AiUserMessageSent/AiToolInvocationRecorded/AiAssistantMessageRecorded pass through Advance
    // unchanged -- the transcript they build is derived on demand from the raw event stream (see
    // AiSessionHistoryBuilder/AiSessionViewBuilder), never carried on this aggregate.
    public static MealplanAiSession Start(MealplanAiSessionEvent @event) => @event switch
    {
        AiSessionStarted started => new MealplanAiSession(
            started.Id,
            started.From,
            started.To,
            [.. started.RequestedSlots],
            ImmutableDictionary<(DateOnly, MealSlot), MealId>.Empty,
            AiSessionStatus.Drafting),
        _ => throw EventReplay.NotAStartEvent(nameof(MealplanAiSession), @event.EventType)
    };

    public static MealplanAiSession Advance(MealplanAiSession session, MealplanAiSessionEvent @event) => @event switch
    {
        AiDraftAssignmentSet set => session with
        {
            Draft = session.Draft.SetItem((set.Date, set.Slot), set.MealId)
        },
        AiDraftAssignmentCleared cleared => session with
        {
            Draft = session.Draft.Remove((cleared.Date, cleared.Slot))
        },
        AiSessionApplied => session with { Status = AiSessionStatus.Applied },
        AiSessionDiscarded => session with { Status = AiSessionStatus.Discarded },
        // The conversation itself is replayed from the stream by AiSessionHistoryBuilder; the
        // session state only tracks the draft and its status.
        AiUserMessageSent or AiToolInvocationRecorded or AiAssistantMessageRecorded => session,
        AiSessionStarted => throw EventReplay.AlreadyStarted(nameof(MealplanAiSession), @event.EventType)
    };
}

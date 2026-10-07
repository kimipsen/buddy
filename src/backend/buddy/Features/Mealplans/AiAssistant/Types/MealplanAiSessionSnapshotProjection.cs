using Marten.Events.Aggregation;

namespace buddy.Features.Mealplans;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct MealplanAiSessionId(Guid Value)" -- as a document's
// Id. MealplanAiSessionId here is a sealed record (a class), which Marten's DocumentMapping
// rejects with "Could not determine an 'id/Id' field or property". So the snapshot document can't
// be MealplanAiSession itself; this thin wrapper carries the plain Guid Marten needs alongside the
// actual MealplanAiSession value. MealplanAiSessionId stays untouched everywhere else in the
// codebase -- this wrapper exists purely at the snapshot-storage boundary. See
// docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record MealplanAiSessionSnapshot(Guid Id, MealplanAiSession MealplanAiSession);

// Inline snapshot of MealplanAiSession, maintained by Marten in the same transaction as every
// event append (see MealplansFeature.AddMealplansFeature:
// options.Projections.Register(new MealplanAiSessionSnapshotProjection(), ...)). Stored in the
// shared "snapshots" schema, never the "mealplans" event schema -- it is derived, rebuildable
// state, not a second source of truth. AiUserMessageSent/AiToolInvocationRecorded/
// AiAssistantMessageRecorded have no case here, the same way MealplanAiSession.Advance passes
// them through unchanged: they don't change the session's own fields, only the transcript that's
// derived on demand from the raw event stream.
public sealed class MealplanAiSessionSnapshotProjection : SingleStreamProjection<MealplanAiSessionSnapshot, Guid>
{
    public static MealplanAiSessionSnapshot Create(AiSessionStarted started) =>
        new(started.Id.Value, MealplanAiSession.Start(MealplanAiSessionEvent.FromPayload(started)));

    public MealplanAiSessionSnapshot Apply(MealplanAiSessionSnapshot current, AiDraftAssignmentSet set) =>
        current with { MealplanAiSession = MealplanAiSession.Advance(current.MealplanAiSession, MealplanAiSessionEvent.FromPayload(set)) };

    public MealplanAiSessionSnapshot Apply(MealplanAiSessionSnapshot current, AiDraftAssignmentCleared cleared) =>
        current with { MealplanAiSession = MealplanAiSession.Advance(current.MealplanAiSession, MealplanAiSessionEvent.FromPayload(cleared)) };

    public MealplanAiSessionSnapshot Apply(MealplanAiSessionSnapshot current, AiSessionApplied applied) =>
        current with { MealplanAiSession = MealplanAiSession.Advance(current.MealplanAiSession, MealplanAiSessionEvent.FromPayload(applied)) };

    public MealplanAiSessionSnapshot Apply(MealplanAiSessionSnapshot current, AiSessionDiscarded discarded) =>
        current with { MealplanAiSession = MealplanAiSession.Advance(current.MealplanAiSession, MealplanAiSessionEvent.FromPayload(discarded)) };

    public MealplanAiSessionSnapshot Apply(MealplanAiSessionSnapshot current, AiSessionExpired expired) =>
        current with { MealplanAiSession = MealplanAiSession.Advance(current.MealplanAiSession, MealplanAiSessionEvent.FromPayload(expired)) };
}

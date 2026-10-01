using Marten.Events.Aggregation;

namespace buddy.Features.Progress;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct ProgressId(Guid Value)" -- as a document's Id.
// ProgressId here is a sealed record (a class), which Marten's DocumentMapping rejects with
// "Could not determine an 'id/Id' field or property". So the snapshot document can't be
// ChildProgress itself; this thin wrapper carries the plain Guid Marten needs alongside the
// actual ChildProgress value. ProgressId stays untouched everywhere else in the codebase -- this
// wrapper exists purely at the snapshot-storage boundary. See
// docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record ChildProgressSnapshot(Guid Id, ChildProgress ChildProgress);

// Inline snapshot of ChildProgress, maintained by Marten in the same transaction as every event
// append (see ProgressFeature.AddProgressFeature: options.Projections.Register(new
// ChildProgressSnapshotProjection(), ...)). Stored in the shared "snapshots" schema, never the
// "progress" event schema -- it is derived, rebuildable state, not a second source of truth.
public sealed class ChildProgressSnapshotProjection : SingleStreamProjection<ChildProgressSnapshot, Guid>
{
    public static ChildProgressSnapshot Create(ProgressStarted started) =>
        new(started.Id.Value, ChildProgress.Fold(null, ProgressEvent.FromPayload(started))!);

    public ChildProgressSnapshot Apply(ChildProgressSnapshot current, StarAwarded awarded) =>
        current with { ChildProgress = ChildProgress.Fold(current.ChildProgress, ProgressEvent.FromPayload(awarded))! };

    public ChildProgressSnapshot Apply(ChildProgressSnapshot current, StarRevoked revoked) =>
        current with { ChildProgress = ChildProgress.Fold(current.ChildProgress, ProgressEvent.FromPayload(revoked))! };

    public ChildProgressSnapshot Apply(ChildProgressSnapshot current, MilestoneUnlocked milestone) =>
        current with { ChildProgress = ChildProgress.Fold(current.ChildProgress, ProgressEvent.FromPayload(milestone))! };

    public ChildProgressSnapshot Apply(ChildProgressSnapshot current, GoalPostsConfigured configured) =>
        current with { ChildProgress = ChildProgress.Fold(current.ChildProgress, ProgressEvent.FromPayload(configured))! };
}

using Marten.Events.Aggregation;

namespace buddy.Features.Pickups;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct PickupScheduleId(Guid Value)" -- as a document's Id.
// PickupScheduleId here is a sealed record (a class), which Marten's DocumentMapping rejects with
// "Could not determine an 'id/Id' field or property". So the snapshot document can't be
// PickupSchedule itself; this thin wrapper carries the plain Guid Marten needs alongside the
// actual PickupSchedule value. PickupScheduleId stays untouched everywhere else in the codebase --
// this wrapper exists purely at the snapshot-storage boundary. See
// docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record PickupScheduleSnapshot(Guid Id, PickupSchedule PickupSchedule);

// Inline snapshot of PickupSchedule, maintained by Marten in the same transaction as every event
// append (see PickupsFeature.AddPickupsFeature: options.Projections.Register(new
// PickupScheduleSnapshotProjection(), ...)). Stored in the shared "snapshots" schema, never the
// "pickups" event schema -- it is derived, rebuildable state, not a second source of truth.
public sealed class PickupScheduleSnapshotProjection : SingleStreamProjection<PickupScheduleSnapshot, Guid>
{
    public static PickupScheduleSnapshot Create(PickupScheduleCreated created) =>
        new(created.Id.Value, PickupSchedule.Fold(null, PickupEvent.FromPayload(created))!);

    public PickupScheduleSnapshot Apply(PickupScheduleSnapshot current, PickupAssigned assigned) =>
        current with { PickupSchedule = PickupSchedule.Fold(current.PickupSchedule, PickupEvent.FromPayload(assigned))! };

    public PickupScheduleSnapshot Apply(PickupScheduleSnapshot current, PickupCleared cleared) =>
        current with { PickupSchedule = PickupSchedule.Fold(current.PickupSchedule, PickupEvent.FromPayload(cleared))! };
}

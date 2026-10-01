using Marten.Events.Aggregation;

namespace buddy.Features.Medicines;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct MedicineId(Guid Value)" -- as a document's Id.
// MedicineId here is a sealed record (a class), which Marten's DocumentMapping rejects with
// "Could not determine an 'id/Id' field or property". So the snapshot document can't be
// MedicineSchedule itself; this thin wrapper carries the plain Guid Marten needs alongside the
// actual MedicineSchedule value. MedicineId stays untouched everywhere else in the codebase --
// this wrapper exists purely at the snapshot-storage boundary. See
// docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record MedicineScheduleSnapshot(Guid Id, MedicineSchedule MedicineSchedule);

// Inline snapshot of MedicineSchedule, maintained by Marten in the same transaction as every
// event append (see MedicinesFeature.AddMedicinesFeature:
// options.Projections.Register(new MedicineScheduleSnapshotProjection(), ...)). Stored in the
// shared "snapshots" schema, never the "medicines" event schema -- it is derived, rebuildable
// state, not a second source of truth.
public sealed class MedicineScheduleSnapshotProjection : SingleStreamProjection<MedicineScheduleSnapshot, Guid>
{
    public static MedicineScheduleSnapshot Create(MedicineScheduleCreated created) =>
        new(created.Id.Value, MedicineSchedule.Fold(null, MedicineEvent.FromPayload(created))!);

    public MedicineScheduleSnapshot Apply(MedicineScheduleSnapshot current, MedicineDetailsUpdated updated) =>
        current with { MedicineSchedule = MedicineSchedule.Fold(current.MedicineSchedule, MedicineEvent.FromPayload(updated))! };

    public MedicineScheduleSnapshot Apply(MedicineScheduleSnapshot current, MedicineScheduleRescheduled rescheduled) =>
        current with { MedicineSchedule = MedicineSchedule.Fold(current.MedicineSchedule, MedicineEvent.FromPayload(rescheduled))! };

    public MedicineScheduleSnapshot Apply(MedicineScheduleSnapshot current, MedicineScheduleStopped stopped) =>
        current with { MedicineSchedule = MedicineSchedule.Fold(current.MedicineSchedule, MedicineEvent.FromPayload(stopped))! };

    public MedicineScheduleSnapshot Apply(MedicineScheduleSnapshot current, DoseStatusChanged changed) =>
        current with { MedicineSchedule = MedicineSchedule.Fold(current.MedicineSchedule, MedicineEvent.FromPayload(changed))! };
}

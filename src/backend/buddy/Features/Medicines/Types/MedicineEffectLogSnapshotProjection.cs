using Marten.Events.Aggregation;

namespace buddy.Features.Medicines;

// Thin wrapper: Marten can't use the sealed-record MedicineEffectLogId as a document Id. See
// docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record MedicineEffectLogSnapshot(Guid Id, MedicineEffectLog MedicineEffectLog);

// Inline snapshot in the shared "snapshots" schema -- derived, rebuildable state.
public sealed class MedicineEffectLogSnapshotProjection : SingleStreamProjection<MedicineEffectLogSnapshot, Guid>
{
    public static MedicineEffectLogSnapshot Create(MedicineEffectLogStarted started) =>
        new(started.Id.Value, MedicineEffectLog.Start(MedicineEffectLogEvent.FromPayload(started)));

    public MedicineEffectLogSnapshot Apply(MedicineEffectLogSnapshot current, MedicineEffectNoteLogged logged) =>
        current with { MedicineEffectLog = MedicineEffectLog.Advance(current.MedicineEffectLog, MedicineEffectLogEvent.FromPayload(logged)) };

    public MedicineEffectLogSnapshot Apply(MedicineEffectLogSnapshot current, MedicineEffectNoteCleared cleared) =>
        current with { MedicineEffectLog = MedicineEffectLog.Advance(current.MedicineEffectLog, MedicineEffectLogEvent.FromPayload(cleared)) };
}

using Marten.Events.Aggregation;

namespace buddy.Features.Medicines;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct MedicineSharingId(Guid Value)" -- as a document's
// Id. MedicineSharingId here is a sealed record (a class), which Marten's DocumentMapping rejects
// with "Could not determine an 'id/Id' field or property". So the snapshot document can't be
// MedicineSharing itself; this thin wrapper carries the plain Guid Marten needs alongside the
// actual MedicineSharing value. MedicineSharingId stays untouched everywhere else in the
// codebase -- this wrapper exists purely at the snapshot-storage boundary. See
// docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record MedicineSharingSnapshot(Guid Id, MedicineSharing MedicineSharing);

// Inline snapshot of MedicineSharing, maintained by Marten in the same transaction as every event
// append (see MedicinesFeature.AddMedicinesFeature:
// options.Projections.Register(new MedicineSharingSnapshotProjection(), ...)). Stored in the
// shared "snapshots" schema, never the "medicines" event schema -- it is derived, rebuildable
// state, not a second source of truth.
public sealed class MedicineSharingSnapshotProjection : SingleStreamProjection<MedicineSharingSnapshot, Guid>
{
    // MedicineSharedWithGroup doubles as both the stream's creation event and a later re-share
    // event -- there's no separate "started" event (see MedicineSharingEvents.cs), matching
    // MedicineSharing.Start (first event) vs MedicineSharing.Advance (a later re-share).
    // Marten's generated Evolver dispatches to this Create overload only for the first event on a
    // stream (no snapshot row yet); any later occurrence of MedicineSharedWithGroup on the same
    // stream (a re-share after an unshare) goes through the Apply overload below instead, exactly
    // mirroring that split.
    public static MedicineSharingSnapshot Create(MedicineSharedWithGroup shared) =>
        new(shared.Id.Value, MedicineSharing.Start(MedicineSharingEvent.FromPayload(shared)));

    public MedicineSharingSnapshot Apply(MedicineSharingSnapshot current, MedicineSharedWithGroup shared) =>
        current with { MedicineSharing = MedicineSharing.Advance(current.MedicineSharing, MedicineSharingEvent.FromPayload(shared)) };

    public MedicineSharingSnapshot Apply(MedicineSharingSnapshot current, MedicineUnsharedFromGroup unshared) =>
        current with { MedicineSharing = MedicineSharing.Advance(current.MedicineSharing, MedicineSharingEvent.FromPayload(unshared)) };
}

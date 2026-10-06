using Marten.Events.Aggregation;

namespace buddy.Features.Mealplans;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct MealPlanId(Guid Value)" -- as a document's Id.
// MealPlanId here is a sealed record (a class), which Marten's DocumentMapping rejects with
// "Could not determine an 'id/Id' field or property". So the snapshot document can't be MealPlan
// itself; this thin wrapper carries the plain Guid Marten needs alongside the actual MealPlan
// value. MealPlanId stays untouched everywhere else in the codebase -- this wrapper exists purely
// at the snapshot-storage boundary. See docs/backend/analysis/event-stream-snapshots.md,
// Question 5.
public sealed record MealPlanSnapshot(Guid Id, MealPlan MealPlan);

// Inline snapshot of MealPlan, maintained by Marten in the same transaction as every event append
// (see MealplansFeature.AddMealplansFeature: options.Projections.Register(new
// MealPlanSnapshotProjection(), ...)). Stored in the shared "snapshots" schema, never the
// "mealplans" event schema -- it is derived, rebuildable state, not a second source of truth.
public sealed class MealPlanSnapshotProjection : SingleStreamProjection<MealPlanSnapshot, Guid>
{
    public static MealPlanSnapshot Create(MealPlanCreated created) =>
        new(created.Id.Value, MealPlan.Start(MealPlanEvent.FromPayload(created)));

    public MealPlanSnapshot Apply(MealPlanSnapshot current, MealAssignedToSlot assigned) =>
        current with { MealPlan = MealPlan.Advance(current.MealPlan, MealPlanEvent.FromPayload(assigned)) };

    public MealPlanSnapshot Apply(MealPlanSnapshot current, MealSlotCleared cleared) =>
        current with { MealPlan = MealPlan.Advance(current.MealPlan, MealPlanEvent.FromPayload(cleared)) };

    public MealPlanSnapshot Apply(MealPlanSnapshot current, MealPlanSharedWithGroup shared) =>
        current with { MealPlan = MealPlan.Advance(current.MealPlan, MealPlanEvent.FromPayload(shared)) };

    public MealPlanSnapshot Apply(MealPlanSnapshot current, MealPlanUnsharedFromGroup unshared) =>
        current with { MealPlan = MealPlan.Advance(current.MealPlan, MealPlanEvent.FromPayload(unshared)) };

    public MealPlanSnapshot Apply(MealPlanSnapshot current, MealPlanSlotTimeSet timeSet) =>
        current with { MealPlan = MealPlan.Advance(current.MealPlan, MealPlanEvent.FromPayload(timeSet)) };

    public MealPlanSnapshot Apply(MealPlanSnapshot current, MealPlanIcalTokenIssued issued) =>
        current with { MealPlan = MealPlan.Advance(current.MealPlan, MealPlanEvent.FromPayload(issued)) };

    public MealPlanSnapshot Apply(MealPlanSnapshot current, MealPlanIcalTokenRevoked revoked) =>
        current with { MealPlan = MealPlan.Advance(current.MealPlan, MealPlanEvent.FromPayload(revoked)) };

    public MealPlanSnapshot Apply(MealPlanSnapshot current, MealPlanEntriesImported imported) =>
        current with { MealPlan = MealPlan.Advance(current.MealPlan, MealPlanEvent.FromPayload(imported)) };

    public MealPlanSnapshot Apply(MealPlanSnapshot current, MealPlanImportReverted reverted) =>
        current with { MealPlan = MealPlan.Advance(current.MealPlan, MealPlanEvent.FromPayload(reverted)) };
}

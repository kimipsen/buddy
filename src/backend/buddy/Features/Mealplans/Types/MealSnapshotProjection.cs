using Marten.Events.Aggregation;

namespace buddy.Features.Mealplans;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct MealId(Guid Value)" -- as a document's Id. MealId
// here is a sealed record (a class), which Marten's DocumentMapping rejects with "Could not
// determine an 'id/Id' field or property". So the snapshot document can't be Meal itself; this
// thin wrapper carries the plain Guid Marten needs alongside the actual Meal value. MealId stays
// untouched everywhere else in the codebase -- this wrapper exists purely at the snapshot-storage
// boundary. See docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record MealSnapshot(Guid Id, Meal Meal);

// Inline snapshot of Meal, maintained by Marten in the same transaction as every event append (see
// MealplansFeature.AddMealplansFeature: options.Projections.Register(new MealSnapshotProjection(),
// ...)). Stored in the shared "snapshots" schema, never the "mealplans" event schema -- it is
// derived, rebuildable state, not a second source of truth.
public sealed class MealSnapshotProjection : SingleStreamProjection<MealSnapshot, Guid>
{
    public static MealSnapshot Create(MealCreated created) =>
        new(created.Id.Value, Meal.Fold(null, MealEvent.FromPayload(created))!);

    public MealSnapshot Apply(MealSnapshot current, MealDetailsUpdated updated) =>
        current with { Meal = Meal.Fold(current.Meal, MealEvent.FromPayload(updated))! };

    public MealSnapshot Apply(MealSnapshot current, MealArchived archived) =>
        current with { Meal = Meal.Fold(current.Meal, MealEvent.FromPayload(archived))! };

    public MealSnapshot Apply(MealSnapshot current, MealRated rated) =>
        current with { Meal = Meal.Fold(current.Meal, MealEvent.FromPayload(rated))! };
}

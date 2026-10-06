using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public interface IMealPlanEventStore
{
    Task<IReadOnlyCollection<MealPlanEvent>> ReadAsync(MealPlanId id, CancellationToken cancellationToken);

    Task<MealPlan?> FindSnapshotAsync(MealPlanId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MealPlanEvent>> CreateAsync(MealPlanId id, IReadOnlyCollection<MealPlanEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(MealPlanId id, IReadOnlyCollection<MealPlanEvent> events, CancellationToken cancellationToken);

    // Writes an import in one transaction: every new meal's stream (each starting with
    // MealCreated) together with the plan's events -- started as a new stream when the first plan
    // event is MealPlanCreated, appended otherwise. Meals and plans share one Marten store, so an
    // import never leaves new meals behind without the plan entries that use them.
    // Writes a revert in one transaction: the plan's MealSlotCleared + MealPlanImportReverted
    // events and a MealArchived for each meal the import created that nothing uses any more, so a
    // failure can't leave the import marked reverted with its meals still in the picker.
    Task RevertImportAsync(MealPlanId id, IReadOnlyCollection<MealPlanEvent> planEvents, IReadOnlyCollection<MealArchived> archivedMeals, CancellationToken cancellationToken);

    Task ImportAsync(MealPlanId id, IReadOnlyCollection<MealPlanEvent> planEvents, IReadOnlyCollection<IReadOnlyCollection<MealEvent>> newMeals, CancellationToken cancellationToken);

    // A MealPlan is a 1:1 singleton per child, provisioned lazily -- null means the child has no
    // plan stream yet (nothing has ever been assigned).
    Task<MealPlanId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken);

    // Resolves which plan (and its anchor child) a group currently has Manage-tier access to, if
    // any -- see docs/backend/analysis/group-owned-mealplans.md.
    Task<GroupSharedMealPlanDocument?> FindGroupSharedAsync(GroupId groupId, CancellationToken cancellationToken);
}

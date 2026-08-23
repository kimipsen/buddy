using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public interface IMealPlanEventStore
{
    Task<IReadOnlyCollection<MealPlanEvent>> ReadAsync(MealPlanId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MealPlanEvent>> CreateAsync(MealPlanId id, IReadOnlyCollection<MealPlanEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(MealPlanId id, IReadOnlyCollection<MealPlanEvent> events, CancellationToken cancellationToken);

    // A MealPlan is a 1:1 singleton per child, provisioned lazily -- null means the child has no
    // plan stream yet (nothing has ever been assigned).
    Task<MealPlanId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken);
}

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public interface IMealEventStore
{
    Task<IReadOnlyCollection<MealEvent>> ReadAsync(MealId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MealEvent>> CreateAsync(MealId id, IReadOnlyCollection<MealEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(MealId id, IReadOnlyCollection<MealEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MealId>> ListIdsForChildAsync(UserId childId, CancellationToken cancellationToken);
}

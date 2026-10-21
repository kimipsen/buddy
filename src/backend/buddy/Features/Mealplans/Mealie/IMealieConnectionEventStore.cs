using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public interface IMealieConnectionEventStore
{
    Task<IReadOnlyCollection<MealieConnectionEvent>> ReadAsync(MealieConnectionId id, CancellationToken cancellationToken);

    Task<MealieConnection?> FindSnapshotAsync(MealieConnectionId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MealieConnectionEvent>> CreateAsync(MealieConnectionId id, IReadOnlyCollection<MealieConnectionEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(MealieConnectionId id, IReadOnlyCollection<MealieConnectionEvent> events, CancellationToken cancellationToken);

    // The connection indexed under this one child, if any. A family's connection is resolved across
    // all its children by MealFamilyResolution.ResolveFamilyMealieConnectionIdAsync.
    Task<MealieConnectionId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken);
}

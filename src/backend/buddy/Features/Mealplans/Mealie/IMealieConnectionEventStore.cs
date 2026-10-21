using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public interface IMealieConnectionEventStore
{
    Task<IReadOnlyCollection<MealieConnectionEvent>> ReadAsync(MealieConnectionId id, CancellationToken cancellationToken);

    Task<MealieConnection?> FindSnapshotAsync(MealieConnectionId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MealieConnectionEvent>> CreateAsync(MealieConnectionId id, IReadOnlyCollection<MealieConnectionEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(MealieConnectionId id, IReadOnlyCollection<MealieConnectionEvent> events, CancellationToken cancellationToken);

    // The connections indexed under this one child, oldest id first: usually none or one, more after
    // two first connects at the same moment or an erasure that passed a sibling's index on. A
    // family's connection is resolved across all its children by
    // MealFamilyResolution.ResolveFamilyMealieConnectionIdAsync.
    Task<IReadOnlyList<MealieConnectionId>> ListIdsForChildAsync(UserId childId, CancellationToken cancellationToken);
}

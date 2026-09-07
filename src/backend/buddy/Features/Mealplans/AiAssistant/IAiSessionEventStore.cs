using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public interface IAiSessionEventStore
{
    Task<IReadOnlyCollection<MealplanAiSessionEvent>> ReadAsync(MealplanAiSessionId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MealplanAiSessionEvent>> CreateAsync(MealplanAiSessionId id, IReadOnlyCollection<MealplanAiSessionEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(MealplanAiSessionId id, IReadOnlyCollection<MealplanAiSessionEvent> events, CancellationToken cancellationToken);

    // The most recently started session anchored under this specific child, if any -- used by
    // AiSessionResolution to find the latest across every child in the family, since sessions
    // (unlike MealPlan/AiProviderCredential) can be superseded by a later one anchored under a
    // different sibling.
    Task<(MealplanAiSessionId Id, DateTimeOffset StartedAt)?> FindLatestForChildAsync(UserId childId, CancellationToken cancellationToken);
}

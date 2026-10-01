using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public interface IAiCredentialEventStore
{
    Task<IReadOnlyCollection<AiProviderCredentialEvent>> ReadAsync(AiCredentialId id, CancellationToken cancellationToken);

    Task<AiProviderCredential?> FindSnapshotAsync(AiCredentialId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AiProviderCredentialEvent>> CreateAsync(AiCredentialId id, IReadOnlyCollection<AiProviderCredentialEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(AiCredentialId id, IReadOnlyCollection<AiProviderCredentialEvent> events, CancellationToken cancellationToken);

    // The credential indexed under this one child, if any -- null means no credential was ever
    // provisioned through this child. A family's credential is resolved across all its children
    // (and the caller's unlinked children) by MealFamilyResolution.ResolveFamilyAiCredentialIdAsync.
    Task<AiCredentialId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken);
}

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public interface IAiCredentialEventStore
{
    Task<IReadOnlyCollection<AiProviderCredentialEvent>> ReadAsync(AiCredentialId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AiProviderCredentialEvent>> CreateAsync(AiCredentialId id, IReadOnlyCollection<AiProviderCredentialEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(AiCredentialId id, IReadOnlyCollection<AiProviderCredentialEvent> events, CancellationToken cancellationToken);

    // An AiProviderCredential is a 1:1 singleton per family, provisioned lazily -- null means the
    // family has never configured an AI provider key yet.
    Task<AiCredentialId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken);
}

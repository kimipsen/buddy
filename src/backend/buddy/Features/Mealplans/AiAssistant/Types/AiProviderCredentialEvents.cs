using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public union AiProviderCredentialEvent(
    AiCredentialsInitialized,
    ProviderApiKeySet,
    ProviderApiKeyRemoved,
    ActiveProviderChanged
)
{
    public static AiProviderCredentialEvent FromPayload(object payload) => payload switch
    {
        AiCredentialsInitialized e => e,
        ProviderApiKeySet e => e,
        ProviderApiKeyRemoved e => e,
        ActiveProviderChanged e => e,
        _ => throw new ArgumentException($"Unknown AI credential event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        AiCredentialsInitialized => nameof(AiCredentialsInitialized),
        ProviderApiKeySet => nameof(ProviderApiKeySet),
        ProviderApiKeyRemoved => nameof(ProviderApiKeyRemoved),
        ActiveProviderChanged => nameof(ActiveProviderChanged),
    };
}

// Lazily appended by the first SetProviderApiKey call for a family with no credential stream yet --
// mirrors MealPlanCreated's lazy-provisioning pattern (see MealPlanEvents.cs). ChildId seeds the
// credential's first index row the same way MealPlanCreated.ChildId does for MealPlanIndexDocument,
// but (also like MealPlanCreated) isn't projected onto the aggregate itself -- the whole family
// shares this one stream, so "whose credential is this" isn't aggregate state.
public sealed record AiCredentialsInitialized(AiCredentialId Id, UserId ChildId, DateTimeOffset OccurredAt);

public sealed record ProviderApiKeySet(AiCredentialId Id, AiProvider Provider, StoredApiKey Key, DateTimeOffset OccurredAt);

public sealed record ProviderApiKeyRemoved(AiCredentialId Id, AiProvider Provider, UserId RemovedBy, DateTimeOffset OccurredAt);

// Provider is null when the removed provider was the active one, leaving the family with no
// active provider until a guardian picks (or adds) another.
public sealed record ActiveProviderChanged(AiCredentialId Id, AiProvider? Provider, UserId ChangedBy, DateTimeOffset OccurredAt);

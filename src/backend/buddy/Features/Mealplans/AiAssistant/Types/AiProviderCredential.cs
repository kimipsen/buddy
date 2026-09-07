using System.Collections.Immutable;

namespace buddy.Features.Mealplans;

// A family-wide singleton, resolved the same way MealPlan is (see MealFamilyResolution) -- one set
// of provider keys shared by every guardian in the family, not owned by the single child whose
// guardian happened to add the first key.
public sealed record AiProviderCredential(
    AiCredentialId Id,
    ImmutableDictionary<AiProvider, StoredApiKey> Providers,
    AiProvider? ActiveProvider)
{
    public static AiProviderCredential? Rehydrate(IEnumerable<AiProviderCredentialEvent> events)
    {
        AiProviderCredential? credential = null;

        foreach (var @event in events)
        {
            credential = @event switch
            {
                AiCredentialsInitialized created => new AiProviderCredential(
                    created.Id,
                    ImmutableDictionary<AiProvider, StoredApiKey>.Empty,
                    null),
                ProviderApiKeySet set => credential! with
                {
                    Providers = credential!.Providers.SetItem(set.Provider, set.Key)
                },
                ProviderApiKeyRemoved removed => credential! with
                {
                    Providers = credential!.Providers.Remove(removed.Provider)
                },
                ActiveProviderChanged changed => credential! with { ActiveProvider = changed.Provider },
                _ => credential
            };
        }

        return credential;
    }
}

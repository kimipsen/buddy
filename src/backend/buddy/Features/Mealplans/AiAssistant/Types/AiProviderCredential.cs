using System.Collections.Immutable;
using buddy.Common.Aggregates;

namespace buddy.Features.Mealplans;

// A family-wide credential, resolved by MealFamilyResolution.ResolveFamilyAiCredentialIdAsync -- one set
// of provider keys shared by every guardian in the family, not owned by the single child whose
// guardian happened to add the first key.
public sealed record AiProviderCredential(
    AiCredentialId Id,
    ImmutableDictionary<AiProvider, StoredApiKey> Providers,
    AiProvider? ActiveProvider)
{
    public static AiProviderCredential? Rehydrate(IEnumerable<AiProviderCredentialEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static AiProviderCredential Replay(IEnumerable<AiProviderCredentialEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so
    // AiProviderCredentialSnapshotProjection can drive the same logic one Marten-delivered event at
    // a time instead of duplicating this switch. Deliberately not named Apply/Create -- those names
    // are a convention JasperFx's projection source generator scans for on any type used as a
    // projection document, and AiProviderCredential is that document (see Question 4/5 in
    // docs/backend/analysis/event-stream-snapshots.md).
    public static AiProviderCredential Start(AiProviderCredentialEvent @event) => @event switch
    {
        AiCredentialsInitialized created => new AiProviderCredential(
            created.Id,
            ImmutableDictionary<AiProvider, StoredApiKey>.Empty,
            null),
        _ => throw EventReplay.NotAStartEvent(nameof(AiProviderCredential), @event.EventType)
    };

    public static AiProviderCredential Advance(AiProviderCredential credential, AiProviderCredentialEvent @event) => @event switch
    {
        ProviderApiKeySet set => credential with
        {
            Providers = credential.Providers.SetItem(set.Provider, set.Key)
        },
        ProviderApiKeyRemoved removed => credential with
        {
            Providers = credential.Providers.Remove(removed.Provider)
        },
        ActiveProviderChanged changed => credential with { ActiveProvider = changed.Provider },
        AiCredentialsInitialized => throw EventReplay.AlreadyStarted(nameof(AiProviderCredential), @event.EventType)
    };
}

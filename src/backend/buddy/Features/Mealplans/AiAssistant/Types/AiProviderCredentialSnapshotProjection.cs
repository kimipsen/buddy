using Marten.Events.Aggregation;

namespace buddy.Features.Mealplans;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct AiCredentialId(Guid Value)" -- as a document's Id.
// AiCredentialId here is a sealed record (a class), which Marten's DocumentMapping rejects with
// "Could not determine an 'id/Id' field or property". So the snapshot document can't be
// AiProviderCredential itself; this thin wrapper carries the plain Guid Marten needs alongside the
// actual AiProviderCredential value. AiCredentialId stays untouched everywhere else in the
// codebase -- this wrapper exists purely at the snapshot-storage boundary. See
// docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record AiProviderCredentialSnapshot(Guid Id, AiProviderCredential AiProviderCredential);

// Inline snapshot of AiProviderCredential, maintained by Marten in the same transaction as every
// event append (see MealplansFeature.AddMealplansFeature:
// options.Projections.Register(new AiProviderCredentialSnapshotProjection(), ...)). Stored in the
// shared "snapshots" schema, never the "mealplans" event schema -- it is derived, rebuildable
// state, not a second source of truth.
public sealed class AiProviderCredentialSnapshotProjection : SingleStreamProjection<AiProviderCredentialSnapshot, Guid>
{
    public static AiProviderCredentialSnapshot Create(AiCredentialsInitialized created) =>
        new(created.Id.Value, AiProviderCredential.Start(AiProviderCredentialEvent.FromPayload(created)));

    public AiProviderCredentialSnapshot Apply(AiProviderCredentialSnapshot current, ProviderApiKeySet set) =>
        current with { AiProviderCredential = AiProviderCredential.Advance(current.AiProviderCredential, AiProviderCredentialEvent.FromPayload(set)) };

    public AiProviderCredentialSnapshot Apply(AiProviderCredentialSnapshot current, ProviderApiKeyRemoved removed) =>
        current with { AiProviderCredential = AiProviderCredential.Advance(current.AiProviderCredential, AiProviderCredentialEvent.FromPayload(removed)) };

    public AiProviderCredentialSnapshot Apply(AiProviderCredentialSnapshot current, ActiveProviderChanged changed) =>
        current with { AiProviderCredential = AiProviderCredential.Advance(current.AiProviderCredential, AiProviderCredentialEvent.FromPayload(changed)) };

    public AiProviderCredentialSnapshot Apply(AiProviderCredentialSnapshot current, ActiveProviderCleared cleared) =>
        current with { AiProviderCredential = AiProviderCredential.Advance(current.AiProviderCredential, AiProviderCredentialEvent.FromPayload(cleared)) };

    public AiProviderCredentialSnapshot Apply(AiProviderCredentialSnapshot current, AiDataSharingAcknowledged acknowledged) =>
        current with { AiProviderCredential = AiProviderCredential.Advance(current.AiProviderCredential, AiProviderCredentialEvent.FromPayload(acknowledged)) };
}

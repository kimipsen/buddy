using Marten.Events.Aggregation;

namespace buddy.Features.Mealplans;

// Wrapper with a plain Guid Id -- see AiProviderCredentialSnapshot.
public sealed record MealieConnectionSnapshot(Guid Id, MealieConnection MealieConnection);

// Inline snapshot of MealieConnection in the shared "snapshots" schema -- see
// MealplansFeature.AddMealplansFeature and docs/backend/analysis/event-stream-snapshots.md.
public sealed class MealieConnectionSnapshotProjection : SingleStreamProjection<MealieConnectionSnapshot, Guid>
{
    public static MealieConnectionSnapshot Create(MealieConnectionInitialized created) =>
        new(created.Id.Value, MealieConnection.Start(MealieConnectionEvent.FromPayload(created)));

    public MealieConnectionSnapshot Apply(MealieConnectionSnapshot current, MealieConnected connected) =>
        current with { MealieConnection = MealieConnection.Advance(current.MealieConnection, MealieConnectionEvent.FromPayload(connected)) };

    public MealieConnectionSnapshot Apply(MealieConnectionSnapshot current, MealieDisconnected disconnected) =>
        current with { MealieConnection = MealieConnection.Advance(current.MealieConnection, MealieConnectionEvent.FromPayload(disconnected)) };
}

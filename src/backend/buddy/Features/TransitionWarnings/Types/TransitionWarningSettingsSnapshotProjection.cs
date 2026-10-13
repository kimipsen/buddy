using Marten.Events.Aggregation;

namespace buddy.Features.TransitionWarnings;

// Marten can't use the sealed-record id as a document id, so the snapshot wraps the aggregate with
// a plain Guid (docs/backend/analysis/event-stream-snapshots.md, Question 5).
public sealed record TransitionWarningSettingsSnapshot(Guid Id, TransitionWarningSettings Settings);

// Inline snapshot in the shared "snapshots" schema, kept consistent with every append.
public sealed class TransitionWarningSettingsSnapshotProjection : SingleStreamProjection<TransitionWarningSettingsSnapshot, Guid>
{
    public static TransitionWarningSettingsSnapshot Create(TransitionWarningsStarted started) =>
        new(started.Id.Value, TransitionWarningSettings.Start(TransitionWarningEvent.FromPayload(started)));

    public TransitionWarningSettingsSnapshot Apply(TransitionWarningSettingsSnapshot current, TransitionWarningDefaultsConfigured configured) =>
        current with { Settings = TransitionWarningSettings.Advance(current.Settings, TransitionWarningEvent.FromPayload(configured)) };

    public TransitionWarningSettingsSnapshot Apply(TransitionWarningSettingsSnapshot current, ItemWarningOverrideSet set) =>
        current with { Settings = TransitionWarningSettings.Advance(current.Settings, TransitionWarningEvent.FromPayload(set)) };

    public TransitionWarningSettingsSnapshot Apply(TransitionWarningSettingsSnapshot current, ItemWarningOverrideCleared cleared) =>
        current with { Settings = TransitionWarningSettings.Advance(current.Settings, TransitionWarningEvent.FromPayload(cleared)) };
}

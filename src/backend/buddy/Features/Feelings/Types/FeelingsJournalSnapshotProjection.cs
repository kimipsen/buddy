using Marten.Events.Aggregation;

namespace buddy.Features.Feelings;

// Thin wrapper: Marten can't use the sealed-record FeelingsJournalId as a document Id. See
// PickupScheduleSnapshot and docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record FeelingsJournalSnapshot(Guid Id, FeelingsJournal FeelingsJournal);

// Inline snapshot in the shared "snapshots" schema -- derived, rebuildable state, never a second
// source of truth.
public sealed class FeelingsJournalSnapshotProjection : SingleStreamProjection<FeelingsJournalSnapshot, Guid>
{
    public static FeelingsJournalSnapshot Create(FeelingsJournalStarted started) =>
        new(started.Id.Value, FeelingsJournal.Start(FeelingsJournalEvent.FromPayload(started)));

    public FeelingsJournalSnapshot Apply(FeelingsJournalSnapshot current, FeelingCheckedIn checkedIn) =>
        current with { FeelingsJournal = FeelingsJournal.Advance(current.FeelingsJournal, FeelingsJournalEvent.FromPayload(checkedIn)) };

    public FeelingsJournalSnapshot Apply(FeelingsJournalSnapshot current, FeelingCheckInEdited edited) =>
        current with { FeelingsJournal = FeelingsJournal.Advance(current.FeelingsJournal, FeelingsJournalEvent.FromPayload(edited)) };

    public FeelingsJournalSnapshot Apply(FeelingsJournalSnapshot current, FeelingCheckInRemoved removed) =>
        current with { FeelingsJournal = FeelingsJournal.Advance(current.FeelingsJournal, FeelingsJournalEvent.FromPayload(removed)) };

    public FeelingsJournalSnapshot Apply(FeelingsJournalSnapshot current, CheckInPromptTimesSet set) =>
        current with { FeelingsJournal = FeelingsJournal.Advance(current.FeelingsJournal, FeelingsJournalEvent.FromPayload(set)) };

    public FeelingsJournalSnapshot Apply(FeelingsJournalSnapshot current, FeelingsDisplayModeSet set) =>
        current with { FeelingsJournal = FeelingsJournal.Advance(current.FeelingsJournal, FeelingsJournalEvent.FromPayload(set)) };
}

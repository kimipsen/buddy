using Marten.Events.Aggregation;

namespace buddy.Features.NightDuty;

// Wrapper document for the same reason as WorkLocationScheduleSnapshot: Marten can't use a
// sealed-record id class as a document Id. See docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record NightScheduleSnapshot(Guid Id, NightSchedule NightSchedule);

// Inline snapshot of NightSchedule, maintained in the same transaction as every event append and
// stored in the shared "snapshots" schema -- derived, rebuildable state.
public sealed class NightScheduleSnapshotProjection : SingleStreamProjection<NightScheduleSnapshot, Guid>
{
    public static NightScheduleSnapshot Create(NightScheduleStarted started) =>
        new(started.Id.Value, NightSchedule.Start(NightDutyEvent.FromPayload(started)));

    public NightScheduleSnapshot Apply(NightScheduleSnapshot current, NightPatternReplaced e) => Next(current, e);

    public NightScheduleSnapshot Apply(NightScheduleSnapshot current, NightOverridden e) => Next(current, e);

    public NightScheduleSnapshot Apply(NightScheduleSnapshot current, NightOverrideCleared e) => Next(current, e);

    private static NightScheduleSnapshot Next(NightScheduleSnapshot current, NightDutyEvent e) =>
        current with { NightSchedule = NightSchedule.Advance(current.NightSchedule, e) };
}

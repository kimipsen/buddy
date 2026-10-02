using Marten.Events.Aggregation;

namespace buddy.Features.WorkLocations;

// Wrapper document for the same reason as PickupScheduleSnapshot: Marten can't use a sealed-record
// id class as a document Id. See docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record WorkLocationScheduleSnapshot(Guid Id, WorkLocationSchedule WorkLocationSchedule);

// Inline snapshot of WorkLocationSchedule, maintained in the same transaction as every event
// append and stored in the shared "snapshots" schema -- derived, rebuildable state.
public sealed class WorkLocationScheduleSnapshotProjection : SingleStreamProjection<WorkLocationScheduleSnapshot, Guid>
{
    public static WorkLocationScheduleSnapshot Create(WorkLocationScheduleStarted started) =>
        new(started.Id.Value, WorkLocationSchedule.Start(WorkLocationEvent.FromPayload(started)));

    public WorkLocationScheduleSnapshot Apply(WorkLocationScheduleSnapshot current, WorkLocationAdded e) => Next(current, e);

    public WorkLocationScheduleSnapshot Apply(WorkLocationScheduleSnapshot current, WorkLocationDetailsChanged e) => Next(current, e);

    public WorkLocationScheduleSnapshot Apply(WorkLocationScheduleSnapshot current, WorkLocationArchived e) => Next(current, e);

    public WorkLocationScheduleSnapshot Apply(WorkLocationScheduleSnapshot current, WorkPatternReplaced e) => Next(current, e);

    public WorkLocationScheduleSnapshot Apply(WorkLocationScheduleSnapshot current, WorkLocationOverridden e) => Next(current, e);

    public WorkLocationScheduleSnapshot Apply(WorkLocationScheduleSnapshot current, WorkLocationOverrideCleared e) => Next(current, e);

    private static WorkLocationScheduleSnapshot Next(WorkLocationScheduleSnapshot current, WorkLocationEvent e) =>
        current with { WorkLocationSchedule = WorkLocationSchedule.Advance(current.WorkLocationSchedule, e) };
}

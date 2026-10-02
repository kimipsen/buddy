namespace buddy.Features.WorkLocations;

public interface IWorkLocationScheduleEventStore
{
    Task<IReadOnlyCollection<WorkLocationEvent>> ReadAsync(WorkLocationScheduleId id, CancellationToken cancellationToken);

    Task<WorkLocationSchedule?> FindSnapshotAsync(WorkLocationScheduleId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WorkLocationEvent>> CreateAsync(WorkLocationScheduleId id, IReadOnlyCollection<WorkLocationEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(WorkLocationScheduleId id, IReadOnlyCollection<WorkLocationEvent> events, CancellationToken cancellationToken);
}

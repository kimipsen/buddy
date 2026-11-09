namespace buddy.Features.NightDuty;

public interface INightScheduleEventStore
{
    Task<IReadOnlyCollection<NightDutyEvent>> ReadAsync(NightScheduleId id, CancellationToken cancellationToken);

    Task<NightSchedule?> FindSnapshotAsync(NightScheduleId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<NightDutyEvent>> CreateAsync(NightScheduleId id, IReadOnlyCollection<NightDutyEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(NightScheduleId id, IReadOnlyCollection<NightDutyEvent> events, CancellationToken cancellationToken);
}

using buddy.Common.Concurrency;

namespace buddy.Features.WorkLocations;

public sealed class MartenWorkLocationScheduleEventStore(IWorkLocationsStore store) : IWorkLocationScheduleEventStore
{
    public async Task<IReadOnlyCollection<WorkLocationEvent>> ReadAsync(WorkLocationScheduleId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => WorkLocationEvent.FromPayload(e.Data))];
    }

    public async Task<WorkLocationSchedule?> FindSnapshotAsync(WorkLocationScheduleId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<WorkLocationScheduleSnapshot>(id.Value, cancellationToken);

        return snapshot?.WorkLocationSchedule;
    }

    public async Task<IReadOnlyCollection<WorkLocationEvent>> CreateAsync(WorkLocationScheduleId id, IReadOnlyCollection<WorkLocationEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not WorkLocationScheduleStarted)
        {
            throw new InvalidOperationException("The first event of a new work location schedule stream must be WorkLocationScheduleStarted.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(WorkLocationScheduleId id, IReadOnlyCollection<WorkLocationEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    private static object[] ToPayloads(IReadOnlyCollection<WorkLocationEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty work location event."))];
}

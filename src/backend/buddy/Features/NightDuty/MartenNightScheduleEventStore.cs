using buddy.Common.Concurrency;

namespace buddy.Features.NightDuty;

public sealed class MartenNightScheduleEventStore(INightDutyStore store) : INightScheduleEventStore
{
    public async Task<IReadOnlyCollection<NightDutyEvent>> ReadAsync(NightScheduleId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => NightDutyEvent.FromPayload(e.Data))];
    }

    public async Task<NightSchedule?> FindSnapshotAsync(NightScheduleId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<NightScheduleSnapshot>(id.Value, cancellationToken);

        return snapshot?.NightSchedule;
    }

    public async Task<IReadOnlyCollection<NightDutyEvent>> CreateAsync(NightScheduleId id, IReadOnlyCollection<NightDutyEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not NightScheduleStarted)
        {
            throw new InvalidOperationException("The first event of a new night schedule stream must be NightScheduleStarted.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(NightScheduleId id, IReadOnlyCollection<NightDutyEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    private static object[] ToPayloads(IReadOnlyCollection<NightDutyEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty night duty event."))];
}

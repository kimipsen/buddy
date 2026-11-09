using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// The read-decide-append steps every write slice shares. The stream is created lazily by the first
// write (NightScheduleStarted bundled into the same CreateAsync), as WorkLocationScheduleWriter
// does. Loading through ReadAsync keeps the append expected-version (StreamVersionTracker).
internal sealed record LoadedNightSchedule(NightSchedule Schedule, bool Exists);

internal static class NightScheduleWriter
{
    public static async Task<LoadedNightSchedule> LoadAsync(INightScheduleEventStore store, UserId childId, CancellationToken cancellationToken)
    {
        var events = await store.ReadAsync(NightScheduleId.ForChild(childId), cancellationToken);

        return NightSchedule.Rehydrate(events) is { } schedule
            ? new LoadedNightSchedule(schedule, Exists: true)
            : new LoadedNightSchedule(NightSchedule.Empty(childId), Exists: false);
    }

    // Persists newEvents (no-op when empty) and returns the schedule with them applied.
    public static async Task<NightSchedule> SaveAsync(
        INightScheduleEventStore store,
        LoadedNightSchedule loaded,
        IReadOnlyList<NightDutyEvent> newEvents,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (newEvents.Count == 0)
        {
            return loaded.Schedule;
        }

        var schedule = loaded.Schedule;

        if (loaded.Exists)
        {
            await store.AppendAsync(schedule.Id, newEvents, cancellationToken);
        }
        else
        {
            await store.CreateAsync(
                schedule.Id,
                [new NightScheduleStarted(schedule.Id, schedule.ChildId, now), .. newEvents],
                cancellationToken);
        }

        return newEvents.Aggregate(schedule, (current, e) => NightSchedule.Advance(current, e));
    }
}

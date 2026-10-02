using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

// The read-decide-append steps every write slice shares. The stream is created lazily by the first
// write (WorkLocationScheduleStarted bundled into the same CreateAsync), the same way
// ConfigureGoalPostsHandler starts a ChildProgress stream -- a guardian who never opens the feature
// has no stream. Loading through ReadAsync keeps the append expected-version (StreamVersionTracker).
internal sealed record LoadedWorkLocationSchedule(WorkLocationSchedule Schedule, bool Exists);

internal static class WorkLocationScheduleWriter
{
    public static async Task<LoadedWorkLocationSchedule> LoadAsync(
        IWorkLocationScheduleEventStore store, UserId guardianId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var events = await store.ReadAsync(WorkLocationScheduleId.ForGuardian(guardianId), cancellationToken);

        return WorkLocationSchedule.Rehydrate(events) is { } schedule
            ? new LoadedWorkLocationSchedule(schedule, Exists: true)
            : new LoadedWorkLocationSchedule(WorkLocationSchedule.Empty(guardianId, now), Exists: false);
    }

    // Persists newEvents (no-op when empty) and returns the schedule with them applied.
    public static async Task<WorkLocationSchedule> SaveAsync(
        IWorkLocationScheduleEventStore store,
        LoadedWorkLocationSchedule loaded,
        IReadOnlyList<WorkLocationEvent> newEvents,
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
                [new WorkLocationScheduleStarted(schedule.Id, schedule.GuardianId, now), .. newEvents],
                cancellationToken);
        }

        return newEvents.Aggregate(schedule, (current, e) => WorkLocationSchedule.Advance(current, e));
    }
}

using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

// Resolves a guardian's WorkDays within [from, to]: override -> pattern -> nothing. Recomputed from
// current aggregate state on every call, never persisted -- the same contract
// PickupScheduleExpansion has. No time zone resolution: dates are the guardian's wall-clock dates.
public static class WorkLocationExpansion
{
    public static async Task<IReadOnlyCollection<WorkDay>> ExpandAsync(
        UserId guardianId,
        DateOnly from,
        DateOnly to,
        IWorkLocationScheduleEventStore store,
        CancellationToken cancellationToken)
    {
        var schedule = await store.FindSnapshotAsync(WorkLocationScheduleId.ForGuardian(guardianId), cancellationToken);

        return schedule is null
            ? EachDate(from, to, date => new WorkDay(date, null, WorkDaySource.None))
            : Expand(schedule, from, to);
    }

    public static IReadOnlyCollection<WorkDay> Expand(WorkLocationSchedule schedule, DateOnly from, DateOnly to) =>
        EachDate(from, to, date => Resolve(schedule, date));

    private static List<WorkDay> EachDate(DateOnly from, DateOnly to, Func<DateOnly, WorkDay> resolve)
    {
        var days = new List<WorkDay>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            days.Add(resolve(date));
        }

        return days;
    }

    private static WorkDay Resolve(WorkLocationSchedule schedule, DateOnly date)
    {
        if (schedule.Overrides.TryGetValue(date, out var @override))
        {
            return new WorkDay(date, Summarize(schedule, @override.LocationId), WorkDaySource.Override);
        }

        return schedule.Pattern.LocationFor(date) is { } patternLocationId
            ? new WorkDay(date, Summarize(schedule, patternLocationId), WorkDaySource.Pattern)
            : new WorkDay(date, null, WorkDaySource.None);
    }

    private static WorkLocationSummary? Summarize(WorkLocationSchedule schedule, WorkLocationId? locationId) =>
        locationId is not null && schedule.FindLocation(locationId) is { } location ? WorkLocationSummary.From(location) : null;
}

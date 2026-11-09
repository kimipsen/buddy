using buddy.Features.Babysitters;
using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// Resolves a child's Nights within [from, to]: override -> the fixed-schedule version in effect ->
// nothing. Recomputed from current aggregate state on every call, never persisted -- the same
// contract WorkLocationExpansion has. Dates are the child's wall-clock dates.
public static class NightExpansion
{
    public static async Task<IReadOnlyCollection<Night>> ExpandAsync(
        UserId childId,
        DateOnly from,
        DateOnly to,
        INightScheduleEventStore store,
        IBabysitterListEventStore babysitters,
        CancellationToken cancellationToken)
    {
        var schedule = await store.FindSnapshotAsync(NightScheduleId.ForChild(childId), cancellationToken)
            ?? NightSchedule.Empty(childId);

        return await ExpandAsync(schedule, from, to, babysitters, cancellationToken);
    }

    public static async Task<IReadOnlyCollection<Night>> ExpandAsync(
        NightSchedule schedule, DateOnly from, DateOnly to, IBabysitterListEventStore babysitters, CancellationToken cancellationToken)
    {
        var resolved = EachDate(from, to).Select(date => Resolve(schedule, date)).ToList();
        var names = await BabysitterNames.LoadAsync(
            resolved.SelectMany(r => new[] { r.Entry, r.Scheduled }).OfType<NightEntry>().BabysitterOwners(),
            babysitters,
            cancellationToken);

        return [.. resolved.Select(r => r.ToNight(names))];
    }

    private static IEnumerable<DateOnly> EachDate(DateOnly from, DateOnly to)
    {
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static ResolvedNight Resolve(NightSchedule schedule, DateOnly date)
    {
        var scheduled = schedule.ScheduledEntryFor(date);

        if (schedule.Overrides.TryGetValue(date, out var @override))
        {
            return @override switch
            {
                NightOverride.Planned planned => new ResolvedNight(date, planned.Entry, NightSource.Override, scheduled),
                NightOverride.Cleared => new ResolvedNight(date, Entry: null, NightSource.Override, scheduled),
            };
        }

        return new ResolvedNight(date, scheduled, NightSource.Pattern, Scheduled: null);
    }

    // Entry null + Source Override = Cleared; Entry null + Source Pattern = Unplanned.
    private sealed record ResolvedNight(DateOnly Date, NightEntry? Entry, NightSource Source, NightEntry? Scheduled)
    {
        public Night ToNight(BabysitterNames names)
        {
            var scheduled = Scheduled is null ? null : NightEntryDto.FromDomain(Scheduled, names);

            NightStatus status = (Entry, Source) switch
            {
                ({ } entry, _) => new NightStatus.NightPlanned(NightEntryDto.FromDomain(entry, names), Source, scheduled),
                (null, NightSource.Override) => new NightStatus.NightCleared(scheduled),
                (null, _) => new NightStatus.NightUnplanned(),
            };

            return new Night(Date, status);
        }
    }
}

internal static class NightBabysitters
{
    // The guardians whose babysitter lists a set of entries refers to, for BabysitterNames.LoadAsync.
    // A union boxes as itself, not as its case, so OfType<NightResponsible.Babysitter>() would never match.
    public static IEnumerable<UserId> BabysitterOwners(this IEnumerable<NightEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Responsible is NightResponsible.Babysitter babysitter)
            {
                yield return babysitter.GuardianId;
            }
        }
    }
}

namespace buddy.Features.SleepDiaries;

// One-off data fix. The Danish date label used to read "Natten til" (the night ending on the
// date), while the English "Night of" and the feelings overview key a night by its evening, so
// every entry logged under the old label sits one day late. Assuming every guardian used the Danish
// label, each entry last logged before the fix went live moves to the day before; entries logged
// after it are already right and stay. A moved entry is re-logged with a timestamp after the
// cutoff, so running the shift again moves nothing. Run by ShiftSleepEntryDatesCommand.
public static class SleepEntryDateShift
{
    public sealed record Plan(IReadOnlyCollection<SleepDiaryEvent> Events, int Moved, IReadOnlyCollection<DateOnly> Conflicts);

    public static Plan For(IReadOnlyCollection<SleepDiaryEvent> events, DateTimeOffset loggedBefore, DateTimeOffset now)
    {
        var diary = SleepDiary.Replay(events);

        var lastLoggedAt = new Dictionary<DateOnly, DateTimeOffset>();
        foreach (var @event in events)
        {
            if (@event is SleepEntryLogged logged)
            {
                lastLoggedAt[logged.Date] = logged.OccurredAt;
            }
        }

        var stale = diary.Entries.Where(e => lastLoggedAt[e.Key] < loggedBefore).ToList();
        var target = diary.Entries.Where(e => lastLoggedAt[e.Key] >= loggedBefore).ToDictionary();

        // A night logged under both labels: leave the whole diary for a guardian to sort out.
        var conflicts = stale.Select(e => e.Key.AddDays(-1)).Where(target.ContainsKey).Order().ToList();
        if (conflicts.Count > 0)
        {
            return new Plan([], 0, conflicts);
        }

        foreach (var (date, entry) in stale)
        {
            target[date.AddDays(-1)] = entry;
        }

        var id = diary.Id;
        List<SleepDiaryEvent> shift = [];
        foreach (var date in diary.Entries.Keys.Union(target.Keys).Order())
        {
            var before = diary.Entries.GetValueOrDefault(date);
            var after = target.GetValueOrDefault(date);

            if (before is not null && after is null)
            {
                shift.Add(new SleepEntryCleared(id, date, before, before.LoggedBy, now));
            }
            // By reference: an entry that stays is the same instance, while a moved one is always
            // re-logged -- even onto an identical night -- so its new timestamp marks it as done.
            else if (after is not null && !ReferenceEquals(before, after))
            {
                shift.Add(new SleepEntryLogged(id, date, before, after, now));
            }
        }

        return new Plan(shift, stale.Count, []);
    }
}

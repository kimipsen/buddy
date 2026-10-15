using buddy.Features.SleepDiaries;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.Features.SleepDiaries.ShiftSleepEntryDates;

// Pure tests of the one-off date shift -- no containers needed.
public sealed class SleepEntryDateShiftTests
{
    private static readonly UserId Child = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly UserId Guardian = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    private static readonly SleepDiaryId Id = SleepDiaryId.ForChild(Child);
    private static readonly DateTimeOffset Before = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset After = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static SleepEntry Night(int minutes) =>
        new(null, null, null, null, null, [], null, false, [], TimeSpan.FromMinutes(minutes), "", Guardian);

    private static SleepDiaryEvent Logged(int day, SleepEntry entry, DateTimeOffset at) =>
        new SleepEntryLogged(Id, new DateOnly(2026, 10, day), null, entry, at);

    private static List<SleepDiaryEvent> Diary(params SleepDiaryEvent[] events) =>
        [new SleepDiaryStarted(Id, Child, Before), .. events];

    private static Dictionary<DateOnly, TimeSpan?> Totals(IEnumerable<SleepDiaryEvent> events) =>
        SleepDiary.Replay(events).Entries.ToDictionary(e => e.Key, e => e.Value.TotalSleepDuration);

    [Fact]
    public void Entries_logged_before_the_cutoff_move_one_day_back()
    {
        var events = Diary(Logged(2, Night(600), Before), Logged(3, Night(540), Before), Logged(5, Night(480), Before));

        var plan = SleepEntryDateShift.For(events, Cutoff, Now);

        Assert.Equal(3, plan.Moved);
        Assert.Empty(plan.Conflicts);
        Assert.Equal(
            new Dictionary<DateOnly, TimeSpan?>
            {
                [new(2026, 10, 1)] = TimeSpan.FromMinutes(600),
                [new(2026, 10, 2)] = TimeSpan.FromMinutes(540),
                [new(2026, 10, 4)] = TimeSpan.FromMinutes(480),
            },
            Totals([.. events, .. plan.Events]));
    }

    [Fact]
    public void Entries_logged_after_the_cutoff_stay()
    {
        var events = Diary(Logged(2, Night(600), Before), Logged(10, Night(540), After));

        var plan = SleepEntryDateShift.For(events, Cutoff, Now);

        Assert.Equal(1, plan.Moved);
        Assert.Equal(
            new Dictionary<DateOnly, TimeSpan?>
            {
                [new(2026, 10, 1)] = TimeSpan.FromMinutes(600),
                [new(2026, 10, 10)] = TimeSpan.FromMinutes(540),
            },
            Totals([.. events, .. plan.Events]));
    }

    [Fact]
    public void An_old_entry_re_edited_after_the_cutoff_counts_as_new()
    {
        var events = Diary(Logged(2, Night(600), Before), new SleepEntryLogged(Id, new(2026, 10, 2), Night(600), Night(610), After));

        var plan = SleepEntryDateShift.For(events, Cutoff, Now);

        Assert.Equal(0, plan.Moved);
        Assert.Empty(plan.Events);
    }

    [Fact]
    public void Running_the_shift_twice_moves_nothing_the_second_time()
    {
        // Identical consecutive nights: the moved entry still gets a fresh SleepEntryLogged.
        var events = Diary(Logged(2, Night(600), Before), Logged(3, Night(600), Before));
        var shifted = (List<SleepDiaryEvent>)[.. events, .. SleepEntryDateShift.For(events, Cutoff, Now).Events];

        var again = SleepEntryDateShift.For(shifted, Cutoff, Now.AddDays(1));

        Assert.Equal(0, again.Moved);
        Assert.Empty(again.Events);
    }

    [Fact]
    public void A_night_logged_under_both_labels_skips_the_whole_diary()
    {
        // Logged as "Natten til 3." before the fix and again as "Natten efter 2." after it.
        var events = Diary(Logged(3, Night(600), Before), Logged(2, Night(600), After), Logged(8, Night(500), Before));

        var plan = SleepEntryDateShift.For(events, Cutoff, Now);

        Assert.Equal([new DateOnly(2026, 10, 2)], plan.Conflicts);
        Assert.Equal(0, plan.Moved);
        Assert.Empty(plan.Events);
    }

    [Fact]
    public void A_cleared_entry_is_not_moved()
    {
        var events = Diary(
            Logged(2, Night(600), Before),
            new SleepEntryCleared(Id, new(2026, 10, 2), Night(600), Guardian, Before));

        var plan = SleepEntryDateShift.For(events, Cutoff, Now);

        Assert.Equal(0, plan.Moved);
        Assert.Empty(plan.Events);
    }
}

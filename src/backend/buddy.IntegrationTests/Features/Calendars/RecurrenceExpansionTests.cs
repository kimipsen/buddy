using buddy.Features.Calendars;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars;

public sealed class RecurrenceExpansionTests
{
    private static readonly DateOnly Seed = new(2025, 6, 1);

    [Fact]
    public void A_one_off_item_occurs_on_its_seed_date_only_when_in_range()
    {
        Assert.Equal([Seed], RecurrenceExpansion.ExpandDates(Seed, new Recurrence.OneOff(), Seed.AddDays(-1), Seed.AddDays(1)));
        Assert.Empty(RecurrenceExpansion.ExpandDates(Seed, new Recurrence.OneOff(), Seed.AddDays(1), Seed.AddDays(5)));
    }

    [Fact]
    public void A_never_ending_rule_is_bounded_by_the_requested_range()
    {
        var daily = new Recurrence.Repeating(RecurrenceFrequency.Daily, 2, new RecurrenceEnd.Never());

        Assert.Equal(
            [Seed.AddDays(2), Seed.AddDays(4)],
            RecurrenceExpansion.ExpandDates(Seed, daily, Seed.AddDays(1), Seed.AddDays(5)));
    }

    [Fact]
    public void An_end_date_is_inclusive()
    {
        var weekly = new Recurrence.Repeating(RecurrenceFrequency.Weekly, 1, new RecurrenceEnd.On(Seed.AddDays(14)));

        Assert.Equal(
            [Seed, Seed.AddDays(7), Seed.AddDays(14)],
            RecurrenceExpansion.ExpandDates(Seed, weekly, Seed, Seed.AddDays(60)));
    }

    [Fact]
    public void A_weekday_filter_drops_the_other_days_including_the_seed()
    {
        var sunday = new DateOnly(2025, 6, 1);
        var schoolDays = new Recurrence.Repeating(
            RecurrenceFrequency.Daily, 1, new RecurrenceEnd.Never(),
            Weekdays.Monday | Weekdays.Tuesday | Weekdays.Wednesday | Weekdays.Thursday | Weekdays.Friday);

        Assert.Equal(
            [sunday.AddDays(1), sunday.AddDays(2), sunday.AddDays(3), sunday.AddDays(4), sunday.AddDays(5), sunday.AddDays(8)],
            RecurrenceExpansion.ExpandDates(sunday, schoolDays, sunday, sunday.AddDays(8)));
    }

    [Fact]
    public void A_daily_rule_on_every_weekday_occurs_every_day()
    {
        var everyDay = new Recurrence.Repeating(RecurrenceFrequency.Daily, 1, new RecurrenceEnd.Never(), Weekdays.All);

        Assert.Equal(10, RecurrenceExpansion.ExpandDates(Seed, everyDay, Seed, Seed.AddDays(9)).Count);
    }

    [Fact]
    public void A_weekly_rule_on_several_days_repeats_them_every_interval_weeks_from_the_seeds_week()
    {
        var wednesday = new DateOnly(2025, 6, 4);
        var monday = wednesday.AddDays(-2);
        var everyOtherWeek = new Recurrence.Repeating(
            RecurrenceFrequency.Weekly, 2, new RecurrenceEnd.Never(), Weekdays.Monday | Weekdays.Thursday);

        // The seed's own week keeps only Thursday (Monday is before the seed); the next week is
        // skipped; the week after has both.
        Assert.Equal(
            [wednesday.AddDays(1), monday.AddDays(14), monday.AddDays(17)],
            RecurrenceExpansion.ExpandDates(wednesday, everyOtherWeek, wednesday, monday.AddDays(20)));
    }

    [Fact]
    public void A_weekly_rule_on_every_day_alternates_whole_weeks()
    {
        var monday = new DateOnly(2025, 6, 2);
        var everyOtherWeek = new Recurrence.Repeating(RecurrenceFrequency.Weekly, 2, new RecurrenceEnd.On(monday.AddDays(20)), Weekdays.All);

        var dates = RecurrenceExpansion.ExpandDates(monday, everyOtherWeek, monday, monday.AddDays(30));

        Assert.Equal([.. Enumerable.Range(0, 7).Select(monday.AddDays), .. Enumerable.Range(14, 7).Select(monday.AddDays)], dates);
    }

    [Fact]
    public void A_weekly_rule_without_weekdays_keeps_the_seeds_weekday()
    {
        var weekly = new Recurrence.Repeating(RecurrenceFrequency.Weekly, 1, new RecurrenceEnd.Never());

        Assert.Equal([Seed, Seed.AddDays(7)], RecurrenceExpansion.ExpandDates(Seed, weekly, Seed, Seed.AddDays(10)));
    }
}

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
}

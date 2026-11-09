using System.Collections.Immutable;

using buddy.Features.NightDuty;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.Features.NightDuty;

// Pure tests of the version and cycle arithmetic -- no containers needed, and no dependency on
// today's date (the HTTP tests can't save a version in the past).
public sealed class NightScheduleResolutionTests
{
    private static readonly UserId Child = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    private static readonly NightEntry Mom = new(new NightResponsible.Guardian(new UserId(Guid.Parse("00000000-0000-0000-0000-000000000001"))), "");
    private static readonly NightEntry Dad = new(new NightResponsible.Guardian(new UserId(Guid.Parse("00000000-0000-0000-0000-000000000003"))), "");

    // Week A Mom on Mondays, week B Dad, anchored on 2026-12-14. ISO week 53 (2026-12-28) is week A
    // and 2027-01-04 (ISO week 1, odd like 53) is week B -- parity would repeat a week here.
    private static readonly NightPattern AlternatingMondays = new(
        new DateOnly(2026, 12, 1), 2, new DateOnly(2026, 12, 14),
        [new NightPatternDay(0, DayOfWeek.Monday, Mom), new NightPatternDay(1, DayOfWeek.Monday, Dad)]);

    [Theory]
    [InlineData("2026-12-14", "Mom")]
    [InlineData("2026-12-21", "Dad")]
    [InlineData("2026-12-28", "Mom")] // ISO week 53
    [InlineData("2027-01-04", "Dad")] // ISO week 1
    [InlineData("2027-01-11", "Mom")]
    [InlineData("2026-12-07", "Dad")] // before the anchor, after EffectiveFrom
    public void An_alternating_schedule_crosses_week_53_and_the_year_boundary(string date, string expected)
    {
        var schedule = NightSchedule.Empty(Child) with { Patterns = [AlternatingMondays] };

        Assert.Equal(expected == "Mom" ? Mom : Dad, schedule.ScheduledEntryFor(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void A_date_before_the_first_version_has_no_entry() =>
        Assert.Null((NightSchedule.Empty(Child) with { Patterns = [AlternatingMondays] }).ScheduledEntryFor(new DateOnly(2026, 11, 30)));

    [Fact]
    public void Each_date_resolves_against_the_latest_version_starting_on_or_before_it()
    {
        var later = new NightPattern(new DateOnly(2027, 1, 4), 1, new DateOnly(2027, 1, 4), [new NightPatternDay(0, DayOfWeek.Monday, Dad)]);
        var schedule = NightSchedule.Empty(Child) with { Patterns = [AlternatingMondays, later] };

        Assert.Equal(Mom, schedule.ScheduledEntryFor(new DateOnly(2026, 12, 28)));
        Assert.Equal(Dad, schedule.ScheduledEntryFor(new DateOnly(2027, 1, 4)));
        Assert.Equal(Dad, schedule.ScheduledEntryFor(new DateOnly(2027, 1, 11)));
    }

    [Fact]
    public void A_new_version_drops_every_version_starting_on_or_after_it()
    {
        var january = AlternatingMondays with { EffectiveFrom = new DateOnly(2027, 1, 4) };
        var february = AlternatingMondays with { EffectiveFrom = new DateOnly(2027, 2, 1) };
        var schedule = NightSchedule.Empty(Child) with { Patterns = [AlternatingMondays, january, february] };

        var replaced = schedule.WithPattern(january with { CycleWeeks = 1 });

        Assert.Equal([new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 4)], replaced.Select(p => p.EffectiveFrom));
        Assert.Equal(1, replaced[^1].CycleWeeks);
    }

    [Fact]
    public void An_entry_with_neither_a_person_nor_a_note_is_empty()
    {
        Assert.True(new NightEntry(new NightResponsible.Unassigned(), "").IsEmpty);
        Assert.False(new NightEntry(new NightResponsible.Unassigned(), "Screen night").IsEmpty);
        Assert.False(Mom.IsEmpty);
        Assert.Equal(ImmutableDictionary<DateOnly, NightOverride>.Empty, NightSchedule.Empty(Child).Overrides);
    }
}

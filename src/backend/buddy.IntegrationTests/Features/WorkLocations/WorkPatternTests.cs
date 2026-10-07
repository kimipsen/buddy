using System.Globalization;

using buddy.Features.WorkLocations;

using Xunit;

namespace buddy.IntegrationTests.Features.WorkLocations;

// Pure tests of the cycle-week arithmetic -- no containers needed.
public sealed class WorkPatternTests
{
    private static readonly WorkLocationId Stil = new(Guid.Parse("00000000-0000-0000-0000-000000000080"));

    [Theory]
    [InlineData("2026-09-28", "2026-09-28")] // Monday
    [InlineData("2026-10-03", "2026-09-28")] // Saturday
    [InlineData("2026-10-04", "2026-09-28")] // Sunday belongs to the week that started the Monday before
    [InlineData("2027-01-01", "2026-12-28")]
    public void MondayOnOrBefore_finds_the_weeks_monday(string date, string expected) =>
        Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), WorkPattern.MondayOnOrBefore(DateOnly.Parse(date, CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData(2, "2026-09-28", 0)]
    [InlineData(2, "2026-10-04", 0)]
    [InlineData(2, "2026-10-05", 1)]
    [InlineData(2, "2026-10-12", 0)]
    [InlineData(2, "2026-09-27", 1)] // the Sunday before the anchor
    [InlineData(2, "2026-09-21", 1)]
    [InlineData(3, "2026-09-14", 1)] // two weeks before the anchor in a three-week cycle
    [InlineData(1, "2030-01-01", 0)]
    public void CycleWeekOf_uses_floored_modulo_from_the_anchor(int cycleWeeks, string date, int expected)
    {
        var pattern = new WorkPattern(cycleWeeks, new DateOnly(2026, 9, 28), []);

        Assert.Equal(expected, pattern.CycleWeekOf(DateOnly.Parse(date, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Normalized_patterns_with_the_same_content_are_the_same()
    {
        var monday = new DateOnly(2026, 9, 28);
        var a = new WorkPattern(2, monday, [new(1, DayOfWeek.Sunday, Stil), new(0, DayOfWeek.Monday, Stil)]).Normalized();
        var b = new WorkPattern(2, monday, [new(0, DayOfWeek.Monday, Stil), new(1, DayOfWeek.Sunday, Stil)]).Normalized();

        Assert.True(a.IsSameAs(b));
        Assert.Equal(DayOfWeek.Monday, a.Days[0].Day);
        Assert.False(a.IsSameAs(a with { CycleWeeks = 3 }));
    }
}

using buddy.Features.FlashCards;

using Xunit;

namespace buddy.IntegrationTests.Features.FlashCards;

public sealed class DanishClockTests
{
    private static readonly string[] HourNames =
        ["tolv", "et", "to", "tre", "fire", "fem", "seks", "syv", "otte", "ni", "ti", "elleve"];

    private static string Name(int hour) => HourNames[hour % 12];

    public static TheoryData<int, int, string> Table()
    {
        TheoryData<int, int, string> data = [];

        foreach (var hour in new[] { 3, 11, 12 })
        {
            var x = Name(hour);
            var next = Name(hour + 1);
            data.Add(hour, 0, $"klokken {x}");
            data.Add(hour, 5, $"fem minutter over {x}");
            data.Add(hour, 10, $"ti minutter over {x}");
            data.Add(hour, 15, $"kvart over {x}");
            data.Add(hour, 20, $"tyve minutter over {x}");
            data.Add(hour, 25, $"fem minutter i halv {next}");
            data.Add(hour, 30, $"halv {next}");
            data.Add(hour, 35, $"fem minutter over halv {next}");
            data.Add(hour, 40, $"ti minutter over halv {next}");
            data.Add(hour, 45, $"kvart i {next}");
            data.Add(hour, 50, $"ti minutter i {next}");
            data.Add(hour, 55, $"fem minutter i {next}");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void Words_follow_the_rule_table(int hour, int minute, string expected) =>
        Assert.Equal(expected, DanishClock.Words(hour, minute));

    [Theory]
    [InlineData(3, 30, "halv fire")]
    [InlineData(3, 45, "kvart i fire")]
    [InlineData(3, 35, "fem minutter over halv fire")]
    [InlineData(3, 50, "ti minutter i fire")]
    [InlineData(12, 0, "klokken tolv")]
    [InlineData(12, 30, "halv et")]
    [InlineData(12, 45, "kvart i et")]
    [InlineData(11, 30, "halv tolv")]
    [InlineData(11, 35, "fem minutter over halv tolv")]
    [InlineData(11, 45, "kvart i tolv")]
    public void Words_match_the_wording_in_the_issue(int hour, int minute, string expected) =>
        Assert.Equal(expected, DanishClock.Words(hour, minute));

    [Theory]
    [InlineData(3, 0, "3:00")]
    [InlineData(3, 5, "3:05")]
    [InlineData(3, 30, "3:30")]
    [InlineData(12, 55, "12:55")]
    [InlineData(1, 10, "1:10")]
    public void Digital_is_twelve_hour_with_two_minute_digits(int hour, int minute, string expected) =>
        Assert.Equal(expected, DanishClock.Digital(hour, minute));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(13, 0)]
    [InlineData(-1, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 7)]
    [InlineData(3, 60)]
    [InlineData(3, -5)]
    public void Invalid_input_is_rejected(int hour, int minute)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DanishClock.Words(hour, minute));
        Assert.Throws<ArgumentOutOfRangeException>(() => DanishClock.Digital(hour, minute));
    }

    [Fact]
    public void All_144_positions_give_unique_non_empty_words()
    {
        var all = new List<string>();

        for (var hour = 1; hour <= 12; hour++)
        {
            for (var minute = 0; minute < 60; minute += 5)
            {
                all.Add(DanishClock.Words(hour, minute));
            }
        }

        Assert.Equal(144, all.Count);
        Assert.All(all, w => Assert.False(string.IsNullOrWhiteSpace(w)));
        Assert.Equal(144, all.Distinct().Count());
    }
}

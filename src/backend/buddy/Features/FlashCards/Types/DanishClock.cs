using System.Globalization;

namespace buddy.Features.FlashCards;

// Pure Danish wording of a 12-hour clock position (D13). The hour is 1-12 and the minute a
// multiple of 5; anything else is a programmer error and throws.
public static class DanishClock
{
    private static readonly string[] HourWords =
        ["tolv", "et", "to", "tre", "fire", "fem", "seks", "syv", "otte", "ni", "ti", "elleve"];

    public static string Words(int hour, int minute)
    {
        Validate(hour, minute);

        var current = HourWords[hour % 12];
        var next = HourWords[(hour + 1) % 12];

        return minute switch
        {
            0 => $"klokken {current}",
            5 => $"fem minutter over {current}",
            10 => $"ti minutter over {current}",
            15 => $"kvart over {current}",
            20 => $"tyve minutter over {current}",
            25 => $"fem minutter i halv {next}",
            30 => $"halv {next}",
            35 => $"fem minutter over halv {next}",
            40 => $"ti minutter over halv {next}",
            45 => $"kvart i {next}",
            50 => $"ti minutter i {next}",
            _ => $"fem minutter i {next}",
        };
    }

    public static string Digital(int hour, int minute)
    {
        Validate(hour, minute);

        return string.Create(CultureInfo.InvariantCulture, $"{hour}:{minute:00}");
    }

    private static void Validate(int hour, int minute)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(hour, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hour, 12);
        ArgumentOutOfRangeException.ThrowIfNegative(minute);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minute, 55);

        if (minute % 5 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minute), minute, "The minute must be a multiple of 5.");
        }
    }
}

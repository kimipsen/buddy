namespace buddy.Features.Calendars;

public sealed record Color(string Value)
{
    public static Color New(string value) => new(value);
}

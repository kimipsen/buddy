namespace buddy.Features.Calendars;

public sealed record Icon(string Value)
{
    public static Icon New(string value) => new(value);
}

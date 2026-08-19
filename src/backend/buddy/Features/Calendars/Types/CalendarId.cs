namespace buddy.Features.Calendars;

public sealed record CalendarId(Guid Value)
{
    public static CalendarId New() => new(Guid.CreateVersion7());
}

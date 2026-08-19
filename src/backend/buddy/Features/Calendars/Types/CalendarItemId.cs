namespace buddy.Features.Calendars;

public sealed record CalendarItemId(Guid Value)
{
    public static CalendarItemId New() => new(Guid.CreateVersion7());
}

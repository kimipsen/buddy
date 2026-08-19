namespace buddy.Features.Calendars;

public static class TimeZoneResolution
{
    public static bool IsValid(TimeZoneId zoneId) => TimeZoneInfo.TryFindSystemTimeZoneById(zoneId.Value, out _);

    // TimeZoneInfo.GetUtcOffset resolves the correct offset for the given local date/time,
    // including DST -- this is what lets a recurring event keep the same wall-clock time across
    // a DST boundary instead of drifting by an hour. Ambiguous/invalid local times that fall in a
    // DST transition gap use .NET's default resolution rather than surfacing a distinct error.
    public static DateTimeOffset ResolveInstant(TimeZoneId zoneId, DateTime local)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId.Value);

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}

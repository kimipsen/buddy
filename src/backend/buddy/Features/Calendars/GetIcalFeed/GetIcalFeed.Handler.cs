using buddy.Common;

namespace buddy.Features.Calendars;

public static class GetIcalFeedHandler
{
    // A rolling window, not "all occurrences ever" -- calendar clients periodically refetch a
    // subscription feed, so this only needs to cover what's relevant right now. Keeps expansion
    // work bounded regardless of how far back a calendar goes or how far a recurrence reaches.
    private static readonly TimeSpan LookBehind = TimeSpan.FromDays(90);
    private static readonly TimeSpan LookAhead = TimeSpan.FromDays(365);

    public static async Task<Result<string>> Handle(
        GetIcalFeed query,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        CancellationToken cancellationToken)
    {
        var calendarEvents = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);

        // Same outcome (no feed) whether the calendar doesn't exist, is deleted, or the token is
        // wrong/revoked -- an anonymous request can't distinguish which, by design.
        if (calendar is null)
        {
            return new Result<string>.NotFound();
        }

        if (calendar.FindMatchingToken(IcalToken.Hash(query.Token)) is null)
        {
            return new Result<string>.NotFound();
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var from = today.AddDays(-LookBehind.Days);
        var to = today.AddDays(LookAhead.Days);

        var occurrences = await CalendarOccurrenceExpansion.ExpandAsync(query.CalendarId, calendar.TimeZoneId, from, to, items, cancellationToken);

        return new Result<string>.Success(IcalFeedWriter.Write(calendar.Name, occurrences));
    }
}

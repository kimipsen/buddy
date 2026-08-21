using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListIcalTokensHandler
{
    public static async Task<ListIcalTokensResult> Handle(ListIcalTokens query, ICalendarEventStore calendars, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new ListIcalTokensResult([], CalendarAccess.NotFound);
        }

        var events = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return new ListIcalTokensResult([], access);
        }

        var tokens = calendar!.Tokens
            .Select(kv => new IcalTokenSummary(kv.Key.Value, kv.Value.IssuedAt))
            .ToArray();

        return new ListIcalTokensResult(tokens, CalendarAccess.Allowed);
    }
}

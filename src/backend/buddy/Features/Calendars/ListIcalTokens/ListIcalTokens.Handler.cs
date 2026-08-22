using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListIcalTokensHandler
{
    public static async Task<Result<IReadOnlyCollection<IcalTokenSummary>>> Handle(ListIcalTokens query, ICalendarEventStore calendars, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<IcalTokenSummary>>.NotFound();
        }

        var events = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access == CalendarAccess.Forbidden ? new Result<IReadOnlyCollection<IcalTokenSummary>>.Forbidden() : new Result<IReadOnlyCollection<IcalTokenSummary>>.NotFound();
        }

        var tokens = calendar!.Tokens
            .Select(kv => new IcalTokenSummary(kv.Key.Value, kv.Value.IssuedAt))
            .ToArray();

        return new Result<IReadOnlyCollection<IcalTokenSummary>>.Success(tokens);
    }
}

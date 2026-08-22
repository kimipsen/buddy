using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListIcalTokensHandler
{
    public static async Task<Result<IReadOnlyCollection<IcalTokenSummary>>> Handle(ListIcalTokens query, ICalendarEventStore calendars, IGroupEventStore groups, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<IcalTokenSummary>>.NotFound();
        }

        var events = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, guardians, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<IcalTokenSummary>>();
        }

        var tokens = calendar!.Tokens
            .Select(kv => new IcalTokenSummary(kv.Key.Value, kv.Value.IssuedAt))
            .ToArray();

        return new Result<IReadOnlyCollection<IcalTokenSummary>>.Success(tokens);
    }
}

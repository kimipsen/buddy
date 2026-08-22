using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class RevokeIcalTokenHandler
{
    public static async Task<Result<Unit>> Handle(RevokeIcalToken command, ICalendarEventStore calendars, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        if (!calendar!.Tokens.ContainsKey(command.TokenId))
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await calendars.AppendAsync(
            command.CalendarId,
            [new IcalTokenRevoked(command.CalendarId, command.TokenId, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}

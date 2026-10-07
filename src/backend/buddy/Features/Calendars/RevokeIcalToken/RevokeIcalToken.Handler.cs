using buddy.Common;
using buddy.Features.Groups;

namespace buddy.Features.Calendars;

public static class RevokeIcalTokenHandler
{
    public static async Task<Result<Unit>> Handle(RevokeIcalToken command, ICalendarEventStore calendars, IGroupEventStore groups, ILogger<RevokeIcalToken> logger, CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);

        if (calendar is null)
        {
            return new Result<Unit>.NotFound();
        }

        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        if (!calendar.Tokens.ContainsKey(command.TokenId))
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await calendars.AppendAsync(
            command.CalendarId,
            [new IcalTokenRevoked(command.CalendarId, command.TokenId, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        logger.IcalTokenRevoked(command.TokenId.Value, command.CalendarId.Value, userId.Value);

        return new Result<Unit>.Success(Unit.Value);
    }
}

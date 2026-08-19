using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class RevokeIcalTokenHandler
{
    public static async Task<CalendarAccess> Handle(RevokeIcalToken command, ICalendarEventStore calendars, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return CalendarAccess.NotFound;
        }

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = CalendarAuthorization.CheckOwner(calendar, userId);

        if (access != CalendarAccess.Allowed)
        {
            return access;
        }

        if (!calendar!.Tokens.ContainsKey(command.TokenId))
        {
            return CalendarAccess.Allowed;
        }

        await calendars.AppendAsync(
            command.CalendarId,
            [new IcalTokenRevoked(command.CalendarId, command.TokenId, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return CalendarAccess.Allowed;
    }
}

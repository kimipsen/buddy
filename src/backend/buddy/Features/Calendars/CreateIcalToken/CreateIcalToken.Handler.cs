using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class CreateIcalTokenHandler
{
    public static async Task<CreateIcalTokenResult> Handle(CreateIcalToken command, ICalendarEventStore calendars, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new CreateIcalTokenResult(null, null, CalendarAccess.NotFound);
        }

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = CalendarAuthorization.CheckOwner(calendar, userId);

        if (access != CalendarAccess.Allowed)
        {
            return new CreateIcalTokenResult(null, null, access);
        }

        var (token, hash) = IcalToken.Generate();
        var tokenId = IcalTokenId.New();

        await calendars.AppendAsync(
            command.CalendarId,
            [new IcalTokenIssued(command.CalendarId, tokenId, hash, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new CreateIcalTokenResult(tokenId, token, CalendarAccess.Allowed);
    }
}

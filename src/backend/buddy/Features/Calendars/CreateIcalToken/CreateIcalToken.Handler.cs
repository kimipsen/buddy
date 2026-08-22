using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class CreateIcalTokenHandler
{
    public static async Task<Result<IssuedIcalToken>> Handle(CreateIcalToken command, ICalendarEventStore calendars, IGroupEventStore groups, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<IssuedIcalToken>.NotFound();
        }

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, guardians, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<IssuedIcalToken>();
        }

        var (token, hash) = IcalToken.Generate();
        var tokenId = IcalTokenId.New();

        await calendars.AppendAsync(
            command.CalendarId,
            [new IcalTokenIssued(command.CalendarId, tokenId, hash, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<IssuedIcalToken>.Success(new IssuedIcalToken(tokenId, token));
    }
}

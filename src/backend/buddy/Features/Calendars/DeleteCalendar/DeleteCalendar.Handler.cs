using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class DeleteCalendarHandler
{
    public static async Task<Result<Unit>> Handle(DeleteCalendar command, ICalendarEventStore calendars, IGroupEventStore groups, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, guardians, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        await calendars.AppendAsync(command.CalendarId, [new CalendarDeleted(command.CalendarId, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class DeleteCalendarHandler
{
    public static async Task<Result<Unit>> Handle(DeleteCalendar command, ICalendarEventStore calendars, IGroupEventStore groups, ILogger<DeleteCalendar> logger, CancellationToken cancellationToken)
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

        await calendars.AppendAsync(command.CalendarId, [new CalendarDeleted(command.CalendarId, userId, DateTimeOffset.UtcNow)], cancellationToken);

        logger.CalendarDeleted(command.CalendarId.Value, userId.Value);

        return new Result<Unit>.Success(Unit.Value);
    }
}

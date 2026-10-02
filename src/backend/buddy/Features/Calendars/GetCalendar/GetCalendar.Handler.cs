using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class GetCalendarHandler
{
    public static async Task<Result<Calendar>> Handle(GetCalendar query, ICalendarEventStore calendars, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var calendar = await calendars.FindSnapshotAsync(query.CalendarId, cancellationToken);

        if (calendar is null)
        {
            return new Result<Calendar>.NotFound();
        }

        var access = await CalendarAuthorization.CheckView(calendar, userId, groups, cancellationToken);

        return access == CalendarAccess.Allowed ? new Result<Calendar>.Success(calendar) : access.ToDeniedResult<Calendar>();
    }
}

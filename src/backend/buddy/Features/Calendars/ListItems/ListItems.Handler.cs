using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListItemsHandler
{
    public static async Task<Result<IReadOnlyCollection<CalendarItem>>> Handle(
        ListItems query,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<CalendarItem>>.NotFound();
        }

        var calendarEvents = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckView(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return new Result<IReadOnlyCollection<CalendarItem>>.NotFound();
        }

        var itemIds = await items.ListIdsForCalendarAsync(query.CalendarId, cancellationToken);
        var loaded = new List<CalendarItem>(itemIds.Count);

        foreach (var itemId in itemIds)
        {
            var itemEvents = await items.ReadAsync(itemId, cancellationToken);

            if (CalendarItem.Rehydrate(itemEvents) is { IsDeleted: false } item)
            {
                loaded.Add(item);
            }
        }

        loaded.Sort((a, b) => a.ScheduleKey.CompareTo(b.ScheduleKey));

        return new Result<IReadOnlyCollection<CalendarItem>>.Success(loaded);
    }
}

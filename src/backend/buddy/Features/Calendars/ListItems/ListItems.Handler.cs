using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListItemsHandler
{
    public static async Task<ListItemsResult> Handle(
        ListItems query,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new ListItemsResult([], CalendarAccess.NotFound);
        }

        var calendarEvents = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = CalendarAuthorization.CheckView(calendar, userId);

        if (access != CalendarAccess.Allowed)
        {
            return new ListItemsResult([], access);
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

        return new ListItemsResult(loaded, CalendarAccess.Allowed);
    }
}

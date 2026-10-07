using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Calendars;

public static class ListItemsHandler
{
    public static async Task<Result<IReadOnlyCollection<CalendarItem>>> Handle(
        ListItems query,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var calendarEvents = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);

        if (calendar is null)
        {
            return new Result<IReadOnlyCollection<CalendarItem>>.NotFound();
        }

        var access = await CalendarAuthorization.CheckView(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<CalendarItem>>();
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

        if (await ChildVisibility.IsChildAsync(userId, guardians, cancellationToken))
        {
            return new Result<IReadOnlyCollection<CalendarItem>>.Success(ChildVisibility.FilterForChild(loaded, userId));
        }

        return new Result<IReadOnlyCollection<CalendarItem>>.Success(loaded);
    }
}

using buddy.Common;
using buddy.Features.Groups;

namespace buddy.Features.Calendars;

public static class UpdateItemDetailsHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        UpdateItemDetails command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);

        if (calendar is null)
        {
            return new Result<CalendarItem>.NotFound();
        }

        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<CalendarItem>();
        }

        var itemEvents = await items.ReadAsync(command.ItemId, cancellationToken);
        var item = CalendarItem.Rehydrate(itemEvents);

        if (item is null || item.IsDeleted || item.CalendarId != command.CalendarId)
        {
            return new Result<CalendarItem>.NotFound();
        }

        var before = new ItemDetails(item.Title, item.Icon, item.Color);
        var after = new ItemDetails(command.Title, command.Icon, command.Color);

        if (before == after)
        {
            return new Result<CalendarItem>.Success(item);
        }

        await items.AppendAsync(command.ItemId, [new ItemDetailsUpdated(command.ItemId, before, after, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<CalendarItem>.Success(item with { Title = command.Title, Icon = command.Icon, Color = command.Color, LastModifiedBy = userId });
    }
}

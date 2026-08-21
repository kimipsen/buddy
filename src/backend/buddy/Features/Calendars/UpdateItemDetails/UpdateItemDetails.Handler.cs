using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class UpdateItemDetailsHandler
{
    public static async Task<UpdateItemResult> Handle(
        UpdateItemDetails command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new UpdateItemResult(null, CalendarAccess.NotFound);
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return new UpdateItemResult(null, access);
        }

        var itemEvents = await items.ReadAsync(command.ItemId, cancellationToken);
        var item = CalendarItem.Rehydrate(itemEvents);

        if (item is null || item.IsDeleted || item.CalendarId != command.CalendarId)
        {
            return new UpdateItemResult(null, CalendarAccess.NotFound);
        }

        var before = new ItemDetails(item.Title, item.Icon, item.Color);
        var after = new ItemDetails(command.Title, command.Icon, command.Color);

        if (before == after)
        {
            return new UpdateItemResult(item, CalendarAccess.Allowed);
        }

        await items.AppendAsync(command.ItemId, [new ItemDetailsUpdated(command.ItemId, before, after, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new UpdateItemResult(item with { Title = command.Title, Icon = command.Icon, Color = command.Color, LastModifiedBy = userId }, CalendarAccess.Allowed);
    }
}

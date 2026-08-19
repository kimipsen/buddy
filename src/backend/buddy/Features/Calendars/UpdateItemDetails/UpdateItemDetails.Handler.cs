using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class UpdateItemDetailsHandler
{
    public static async Task<UpdateItemResult> Handle(
        UpdateItemDetails command,
        IUserEventStore users,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        CancellationToken cancellationToken)
    {
        var userId = await users.FindUserIdAsync(command.Subject, cancellationToken);

        if (userId is null)
        {
            return new UpdateItemResult(null, CalendarAccess.NotFound);
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = CalendarAuthorization.CheckContribute(calendar, userId);

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

        await items.AppendAsync(command.ItemId, [new ItemDetailsUpdated(command.ItemId, before, after, DateTimeOffset.UtcNow)], cancellationToken);

        return new UpdateItemResult(item with { Title = command.Title, Icon = command.Icon, Color = command.Color }, CalendarAccess.Allowed);
    }
}

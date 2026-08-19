using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class CreateItemHandler
{
    public static async Task<CreateItemResult> Handle(
        CreateItem command,
        IUserEventStore users,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        CancellationToken cancellationToken)
    {
        if (command.Recurrence is { IntervalCount: < 1 })
        {
            return new CreateItemResult(null, CalendarAccess.Allowed, "Recurrence interval count must be at least 1.");
        }

        var userId = await users.FindUserIdAsync(command.Subject, cancellationToken);

        if (userId is null)
        {
            return new CreateItemResult(null, CalendarAccess.NotFound);
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = CalendarAuthorization.CheckContribute(calendar, userId);

        if (access != CalendarAccess.Allowed)
        {
            return new CreateItemResult(null, access);
        }

        var itemId = CalendarItemId.New();
        var now = DateTimeOffset.UtcNow;
        CalendarItemEvent created;

        if (command.Kind == CalendarItemKind.Event)
        {
            if (command.Period is null)
            {
                return new CreateItemResult(null, CalendarAccess.Allowed, "An event requires both a start and an end time.");
            }

            if (command.Period.EndsAt.Date.ToDateTime(command.Period.EndsAt.Time) <= command.Period.StartsAt.Date.ToDateTime(command.Period.StartsAt.Time))
            {
                return new CreateItemResult(null, CalendarAccess.Allowed, "An event's end time must be after its start time.");
            }

            created = new EventItemCreated(itemId, command.CalendarId, userId, command.Title, command.Icon, command.Color, command.Period, command.Recurrence, now);
        }
        else
        {
            if (command.DueDate is null)
            {
                return new CreateItemResult(null, CalendarAccess.Allowed, "A task requires a due date.");
            }

            created = new TaskItemCreated(itemId, command.CalendarId, userId, command.Title, command.Icon, command.Color, command.DueDate, command.Recurrence, now);
        }

        var events = await items.CreateAsync(itemId, [created], cancellationToken);

        return new CreateItemResult(CalendarItem.Rehydrate(events), CalendarAccess.Allowed);
    }
}

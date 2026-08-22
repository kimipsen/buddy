using System.Diagnostics;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class CreateItemHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        CreateItem command,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (command.Recurrence is { IntervalCount: < 1 })
        {
            return new Result<CalendarItem>.Validation("Recurrence interval count must be at least 1.");
        }

        if (command.UserId is not { } userId)
        {
            return new Result<CalendarItem>.NotFound();
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<CalendarItem>();
        }

        var itemId = CalendarItemId.New();
        var now = DateTimeOffset.UtcNow;
        CalendarItemEvent created;

        if (command.Kind == CalendarItemKind.Event)
        {
            if (command.StartsAt is null || command.EndsAt is null)
            {
                return new Result<CalendarItem>.Validation("An event requires both a start and an end time.");
            }

            var periodResult = Period.TryCreate(command.StartsAt, command.EndsAt);

            if (periodResult is not PeriodValidationResult.Valid(var period))
            {
                return new Result<CalendarItem>.Validation(periodResult switch
                {
                    PeriodValidationResult.Invalid(var message) => message,
                    PeriodValidationResult.Valid => throw new UnreachableException("Already excluded by the enclosing check."),
                });
            }

            created = new EventItemCreated(itemId, command.CalendarId, userId, command.Title, command.Icon, command.Color, period, command.Recurrence, now);
        }
        else
        {
            if (command.DueDate is null)
            {
                return new Result<CalendarItem>.Validation("A task requires a due date.");
            }

            created = new TaskItemCreated(itemId, command.CalendarId, userId, command.Title, command.Icon, command.Color, command.DueDate, command.Recurrence, now);
        }

        var events = await items.CreateAsync(itemId, [created], cancellationToken);

        return new Result<CalendarItem>.Success(CalendarItem.Rehydrate(events)!);
    }
}

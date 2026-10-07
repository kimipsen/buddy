using System.Diagnostics;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;

using FluentValidation;

namespace buddy.Features.Calendars;

public static class CreateItemHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        CreateItem command,
        IValidator<CreateItem> validator,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<CalendarItem>.Validation(problem);
        }

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

        var itemId = CalendarItemId.New();
        var now = DateTimeOffset.UtcNow;
        CalendarItemEvent created;

        switch (command.Schedule)
        {
            case NewItemSchedule.Event @event:
                // The end-after-start invariant is already enforced by CreateItemValidator --
                // Period.TryCreate is called again here purely to obtain the Period value.
                if (Period.TryCreate(@event.StartsAt, @event.EndsAt, @event.IsAllDay) is not PeriodValidationResult.Valid(var period))
                {
                    throw new UnreachableException("CreateItemValidator already guarantees this succeeds.");
                }

                created = new EventItemCreated(itemId, command.CalendarId, userId, command.Title, command.Icon, command.Color, period, command.Recurrence, now);
                break;

            case NewItemSchedule.Task task:
                // Needs the calendar loaded above and runs after authorization, like every other
                // state-dependent check in this handler.
                if (task.AssignedTo is { } assignedTo
                    && await CalendarAuthorization.CheckView(calendar, assignedTo, groups, cancellationToken) != CalendarAccess.Allowed)
                {
                    return new Result<CalendarItem>.Validation(ValidationProblem.Of("The assigned person doesn't have access to this calendar."));
                }

                created = new TaskItemCreated(itemId, command.CalendarId, userId, command.Title, command.Icon, command.Color, task.DueDate, command.Recurrence, now, task.AssignedTo);
                break;

            default:
                throw new UnreachableException("NewItemSchedule holds no case.");
        }

        var events = await items.CreateAsync(itemId, [created], cancellationToken);

        return new Result<CalendarItem>.Success(CalendarItem.Replay(events));
    }
}

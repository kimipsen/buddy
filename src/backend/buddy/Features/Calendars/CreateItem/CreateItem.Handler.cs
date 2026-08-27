using System.Diagnostics;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

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
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<CalendarItem>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<CalendarItem>.NotFound();
        }

        var calendarEvents = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, guardians, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<CalendarItem>();
        }

        // AssignedTo requiring a Task (not an Event) is already enforced by CreateItemValidator;
        // this access check stays here because it needs the calendar loaded above and runs after
        // authorization, like every other state-dependent check in this handler.
        if (command.AssignedTo is { } assignedTo)
        {
            var assigneeAccess = await CalendarAuthorization.CheckView(calendar, assignedTo, groups, guardians, cancellationToken);

            if (assigneeAccess != CalendarAccess.Allowed)
            {
                return new Result<CalendarItem>.Validation(ValidationProblem.Of("The assigned person doesn't have access to this calendar."));
            }
        }

        var itemId = CalendarItemId.New();
        var now = DateTimeOffset.UtcNow;
        CalendarItemEvent created;

        if (command.Kind == CalendarItemKind.Event)
        {
            // StartsAt/EndsAt presence and the end-after-start invariant are already enforced by
            // CreateItemValidator -- Period.TryCreate is called again here purely to obtain the
            // Period value, and is guaranteed to succeed.
            if (Period.TryCreate(command.StartsAt!, command.EndsAt!, command.IsAllDay) is not PeriodValidationResult.Valid(var period))
            {
                throw new UnreachableException("CreateItemValidator already guarantees this succeeds.");
            }

            created = new EventItemCreated(itemId, command.CalendarId, userId, command.Title, command.Icon, command.Color, period, command.Recurrence, now);
        }
        else
        {
            // DueDate presence is already enforced by CreateItemValidator.
            var dueDate = command.DueDate! with { IsAllDay = command.IsAllDay };

            created = new TaskItemCreated(itemId, command.CalendarId, userId, command.Title, command.Icon, command.Color, dueDate, command.Recurrence, now, command.AssignedTo);
        }

        var events = await items.CreateAsync(itemId, [created], cancellationToken);

        return new Result<CalendarItem>.Success(CalendarItem.Rehydrate(events)!);
    }
}

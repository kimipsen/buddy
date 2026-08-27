using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Calendars;

public static class UpdateItemRecurrenceHandler
{
    public static async Task<Result<CalendarItem>> Handle(
        UpdateItemRecurrence command,
        IValidator<UpdateItemRecurrence> validator,
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

        var itemEvents = await items.ReadAsync(command.ItemId, cancellationToken);
        var item = CalendarItem.Rehydrate(itemEvents);

        if (item is null || item.IsDeleted || item.CalendarId != command.CalendarId)
        {
            return new Result<CalendarItem>.NotFound();
        }

        if (item.Recurrence == command.Recurrence)
        {
            return new Result<CalendarItem>.Success(item);
        }

        await items.AppendAsync(
            command.ItemId,
            [new RecurrenceUpdated(command.ItemId, item.Recurrence, command.Recurrence, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<CalendarItem>.Success(item with { Recurrence = command.Recurrence, LastModifiedBy = userId });
    }
}

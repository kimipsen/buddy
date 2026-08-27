using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Calendars;

public static class UpdateCalendarIconHandler
{
    public static async Task<Result<Calendar>> Handle(
        UpdateCalendarIcon command,
        IValidator<UpdateCalendarIcon> validator,
        ICalendarEventStore calendars,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<Calendar>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<Calendar>.NotFound();
        }

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, guardians, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<Calendar>();
        }

        if (calendar!.Icon == command.Icon)
        {
            // Idempotent, same rationale as TransferCalendarToGroupHandler's already-there check.
            return new Result<Calendar>.Success(calendar);
        }

        await calendars.AppendAsync(
            command.CalendarId,
            [new CalendarIconChanged(command.CalendarId, command.Icon, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Calendar>.Success(calendar with { Icon = command.Icon });
    }
}

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class UpdateCalendarIconHandler
{
    public static async Task<Result<Calendar>> Handle(
        UpdateCalendarIcon command, ICalendarEventStore calendars, IGroupEventStore groups, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Calendar>.NotFound();
        }

        if (string.IsNullOrWhiteSpace(command.Icon.Value))
        {
            return new Result<Calendar>.Validation("Icon must not be empty.");
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

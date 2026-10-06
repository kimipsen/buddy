using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class SetMemberRoleHandler
{
    public static async Task<Result<Unit>> Handle(SetMemberRole command, ICalendarEventStore calendars, IGroupEventStore groups, ILogger<SetMemberRole> logger, CancellationToken cancellationToken)
    {
        if (command.Role == CalendarRole.Owner)
        {
            // Ownership is assigned only at creation and never granted through this endpoint.
            return new Result<Unit>.Forbidden();
        }

        var userId = command.UserId;

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);

        if (calendar is null)
        {
            return new Result<Unit>.NotFound();
        }

        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        if (command.MemberId == userId)
        {
            // The owner's own role can't be changed through this endpoint either.
            return new Result<Unit>.Forbidden();
        }

        await calendars.AppendAsync(
            command.CalendarId,
            [new MemberRoleGranted(command.CalendarId, command.MemberId, command.Role, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        logger.CalendarMemberRoleSet(command.MemberId.Value, command.CalendarId.Value, command.Role, userId.Value);

        return new Result<Unit>.Success(Unit.Value);
    }
}

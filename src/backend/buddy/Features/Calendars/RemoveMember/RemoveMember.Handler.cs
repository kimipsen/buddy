using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class RemoveMemberHandler
{
    public static async Task<Result<Unit>> Handle(RemoveMember command, ICalendarEventStore calendars, IGroupEventStore groups, ILogger<RemoveMember> logger, CancellationToken cancellationToken)
    {
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
            // The owner can't remove themselves -- deleting the calendar is the only way to end it.
            return new Result<Unit>.Forbidden();
        }

        if (!calendar.Members.ContainsKey(command.MemberId))
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await calendars.AppendAsync(
            command.CalendarId,
            [new MemberRoleRevoked(command.CalendarId, command.MemberId, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        logger.CalendarMemberRemoved(command.MemberId.Value, command.CalendarId.Value, userId.Value);

        return new Result<Unit>.Success(Unit.Value);
    }
}

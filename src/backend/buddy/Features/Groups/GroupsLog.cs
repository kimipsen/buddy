namespace buddy.Features.Groups;

// Group membership changes -- see docs/backend/observability.md. IDs only: never an email
// address or group name.
internal static partial class GroupsLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Group invite {InviteId} ({Role}) to group {GroupId} sent by {UserId}")]
    public static partial void GroupInviteSent(this ILogger logger, Guid inviteId, GroupRole role, Guid groupId, Guid userId);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Group invite {InviteId} accepted by {UserId}: now {Role} of group {GroupId}")]
    public static partial void GroupInviteAccepted(this ILogger logger, Guid inviteId, Guid userId, GroupRole role, Guid groupId);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning, Message = "Group invite {InviteId} refused for {UserId}: the account's email is unverified or isn't the invited address")]
    public static partial void GroupInviteEmailMismatch(this ILogger logger, Guid inviteId, Guid userId);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Information, Message = "Group invite {InviteId} to group {GroupId} revoked by {UserId}")]
    public static partial void GroupInviteRevoked(this ILogger logger, Guid inviteId, Guid groupId, Guid userId);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Information, Message = "Member {MemberId} removed from group {GroupId} by {UserId}")]
    public static partial void GroupMemberRemoved(this ILogger logger, Guid memberId, Guid groupId, Guid userId);

    [LoggerMessage(EventId = 3006, Level = LogLevel.Information, Message = "Member {MemberId} of group {GroupId} set to {Role} by {UserId}")]
    public static partial void GroupMemberRoleSet(this ILogger logger, Guid memberId, Guid groupId, GroupRole role, Guid userId);

    [LoggerMessage(EventId = 3007, Level = LogLevel.Information, Message = "Group {GroupId} deleted by {UserId}, with {CalendarCount} group-owned calendar(s)")]
    public static partial void GroupDeleted(this ILogger logger, Guid groupId, Guid userId, int calendarCount);
}

namespace buddy.Features.Calendars;

// Calendar access changes and the swallowed side effect in SetTaskCompletion -- see
// docs/backend/observability.md. IDs only: never a token, title or note.
internal static partial class CalendarsLog
{
    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Member {MemberId} removed from calendar {CalendarId} by {UserId}")]
    public static partial void CalendarMemberRemoved(this ILogger logger, Guid memberId, Guid calendarId, Guid userId);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Member {MemberId} of calendar {CalendarId} set to {Role} by {UserId}")]
    public static partial void CalendarMemberRoleSet(this ILogger logger, Guid memberId, Guid calendarId, CalendarRole role, Guid userId);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information, Message = "Calendar {CalendarId} deleted by {UserId}")]
    public static partial void CalendarDeleted(this ILogger logger, Guid calendarId, Guid userId);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Information, Message = "iCal feed token {TokenId} for calendar {CalendarId} issued by {UserId}")]
    public static partial void IcalTokenIssued(this ILogger logger, Guid tokenId, Guid calendarId, Guid userId);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Information, Message = "iCal feed token {TokenId} for calendar {CalendarId} revoked by {UserId}")]
    public static partial void IcalTokenRevoked(this ILogger logger, Guid tokenId, Guid calendarId, Guid userId);

    [LoggerMessage(EventId = 4006, Level = LogLevel.Warning, Message = "Recording the star change for child {ChildId} on item {ItemId} failed; their star count stays stale until the next completion change")]
    public static partial void StarChangeFailed(this ILogger logger, Exception exception, Guid childId, Guid itemId);
}

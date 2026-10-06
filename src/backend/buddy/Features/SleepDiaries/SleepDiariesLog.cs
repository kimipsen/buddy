namespace buddy.Features.SleepDiaries;

// Sharing a child's sleep diary outside Buddy -- health data, so every link and every view is
// logged. See docs/backend/observability.md. IDs only: never the token or the diary itself.
internal static partial class SleepDiariesLog
{
    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Sleep diary share link {ShareLinkId} for child {ChildId} created by {UserId}, expires {ExpiresAt} (empty: never)")]
    public static partial void ShareLinkCreated(this ILogger logger, Guid shareLinkId, Guid childId, Guid userId, DateTimeOffset? expiresAt);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "Sleep diary share link {ShareLinkId} for child {ChildId} revoked by {UserId}")]
    public static partial void ShareLinkRevoked(this ILogger logger, Guid shareLinkId, Guid childId, Guid userId);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Information, Message = "Shared sleep diary of child {ChildId} viewed through link {ShareLinkId}")]
    public static partial void SharedDiaryViewed(this ILogger logger, Guid childId, Guid shareLinkId);
}

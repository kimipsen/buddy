using buddy.Common.Observability;

namespace buddy.Features.Feelings;

// Sharing a child's feelings check-ins outside Buddy, and every read of them inside -- mental-health
// data, so every link and every read is logged (GDPR Question 7; docs/backend/observability.md).
// IDs only: never the token, a zone, a tag or a note.
internal static partial class FeelingsLog
{
    [LoggerMessage(EventId = 11001, Level = LogLevel.Information, Message = "Feelings share link {ShareLinkId} for child {ChildId} created by {UserId}, expires {ExpiresAt} (empty: never)")]
    public static partial void ShareLinkCreated(this ILogger logger, Guid shareLinkId, Guid childId, Guid userId, DateTimeOffset? expiresAt);

    [LoggerMessage(EventId = 11002, Level = LogLevel.Information, Message = "Feelings share link {ShareLinkId} for child {ChildId} revoked by {UserId}")]
    public static partial void ShareLinkRevoked(this ILogger logger, Guid shareLinkId, Guid childId, Guid userId);

    [LoggerMessage(EventId = 11003, Level = LogLevel.Information, Message = "Shared feelings of child {ChildId} viewed through link {ShareLinkId}")]
    public static partial void SharedFeelingsViewed(this ILogger logger, Guid childId, Guid shareLinkId);

    [LoggerMessage(EventId = 11004, Level = LogLevel.Information, Message = "Feelings check-ins of child {ChildId} from {From} to {To} read by {UserId} as {AccessPath}")]
    public static partial void CheckInsRead(this ILogger logger, Guid childId, DateOnly from, DateOnly to, Guid userId, HealthDataAccessPath accessPath);
}

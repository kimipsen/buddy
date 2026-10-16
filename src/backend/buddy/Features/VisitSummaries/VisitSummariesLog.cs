using buddy.Common.Observability;

namespace buddy.Features.VisitSummaries;

// A report combines a child's sleep, medicine and feelings data -- health data, so every link and
// every read is logged (GDPR Question 7). See docs/backend/observability.md. IDs only: never the
// token, the note or any of the report.
internal static partial class VisitSummariesLog
{
    [LoggerMessage(EventId = 12001, Level = LogLevel.Information, Message = "Visit summary {SummaryId} share link {ShareLinkId} for child {ChildId} created by {UserId}, expires {ExpiresAt} (empty: never)")]
    public static partial void ShareLinkCreated(this ILogger logger, Guid summaryId, Guid shareLinkId, Guid childId, Guid userId, DateTimeOffset? expiresAt);

    [LoggerMessage(EventId = 12002, Level = LogLevel.Information, Message = "Visit summary {SummaryId} share link {ShareLinkId} for child {ChildId} revoked by {UserId}")]
    public static partial void ShareLinkRevoked(this ILogger logger, Guid summaryId, Guid shareLinkId, Guid childId, Guid userId);

    [LoggerMessage(EventId = 12003, Level = LogLevel.Information, Message = "Shared visit summary {SummaryId} of child {ChildId} viewed through link {ShareLinkId}")]
    public static partial void SharedReportViewed(this ILogger logger, Guid summaryId, Guid childId, Guid shareLinkId);

    [LoggerMessage(EventId = 12004, Level = LogLevel.Information, Message = "Visit summary {SummaryId} of child {ChildId} read by {UserId} as {AccessPath}")]
    public static partial void ReportRead(this ILogger logger, Guid summaryId, Guid childId, Guid userId, HealthDataAccessPath accessPath);
}

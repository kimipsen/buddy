namespace buddy.Features.Privacy;

// Erasure progress -- see docs/backend/analysis/gdpr-data-protection.md and
// docs/backend/observability.md. IDs only.
internal static partial class PrivacyLog
{
    [LoggerMessage(EventId = 10001, Level = LogLevel.Information, Message = "Erased user {UserId} (child: {IsChild})")]
    public static partial void UserErased(this ILogger logger, Guid userId, bool isChild);

    [LoggerMessage(EventId = 10002, Level = LogLevel.Information, Message = "Erasing child {ChildId}, left without a guardian by the deletion of {GuardianId}")]
    public static partial void OrphanedChildErased(this ILogger logger, Guid childId, Guid guardianId);

    [LoggerMessage(EventId = 10003, Level = LogLevel.Information, Message = "Family data anchored to erased child {ChildId} passes to sibling {HeirId}")]
    public static partial void FamilyDataPassedToHeir(this ILogger logger, Guid childId, Guid heirId);

    [LoggerMessage(EventId = 10004, Level = LogLevel.Warning, Message = "Erasure of user {UserId} stopped before it finished; the user is locked out and UserErasureService will finish it")]
    public static partial void ErasureIncomplete(this ILogger logger, Exception exception, Guid userId);

    [LoggerMessage(EventId = 10005, Level = LogLevel.Error, Message = "An erasure sweep failed")]
    public static partial void ErasureSweepFailed(this ILogger logger, Exception exception);
}

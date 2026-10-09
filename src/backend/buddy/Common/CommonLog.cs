namespace buddy.Common;

// Log lines of the cross-cutting middleware and services in Common/ -- see
// docs/backend/observability.md. Method and path only, never a query string, subject or token.
internal static partial class CommonLog
{
    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Concurrent modification on {Method} {Path}")]
    public static partial void ConcurrentModification(this ILogger logger, Exception exception, string method, string? path);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Information, Message = "Unbindable request on {Method} {Path}")]
    public static partial void UnbindableRequest(this ILogger logger, Exception exception, string method, string? path);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Information, Message = "Deleted {Count} expired idempotency record(s).")]
    public static partial void IdempotencyRecordsDeleted(this ILogger logger, int count);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Error, Message = "Idempotency cleanup pass failed.")]
    public static partial void IdempotencyCleanupFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 8005, Level = LogLevel.Information, Message = "Rate limit exceeded for {CallerKind} caller on {Endpoint}")]
    public static partial void RateLimitExceeded(this ILogger logger, string callerKind, string endpoint);

    [LoggerMessage(EventId = 8006, Level = LogLevel.Information, Message = "Feature flags {Flags} are off because their parent feature is off.")]
    public static partial void SubFlagsFollowParent(this ILogger logger, IEnumerable<string> flags);
}

namespace buddy.Features.Users;

// Account lifecycle events, for auditing and support -- see docs/backend/observability.md.
// IDs only: never an email address, name or token.
internal static partial class UsersLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Provisioned user {UserId} on first sign-in (email verified: {EmailVerified})")]
    public static partial void UserProvisioned(this ILogger logger, Guid userId, bool emailVerified);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "User {UserId} deleted their account")]
    public static partial void UserDeleted(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "User {UserId} changed their email address; verification requested")]
    public static partial void EmailChanged(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "User {UserId} verified their email address")]
    public static partial void EmailVerified(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "Email verification for user {UserId} rejected: {Reason}")]
    public static partial void EmailVerificationRejected(this ILogger logger, Guid userId, string reason);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Rejected {Method} {Path}: the caller has no Buddy user yet")]
    public static partial void UnprovisionedCallerRejected(this ILogger logger, string method, string? path);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning, Message = "Deleting the Keycloak account of deleted user {UserId} failed; the user stays locked out of Buddy")]
    public static partial void KeycloakAccountDeletionFailed(this ILogger logger, Exception exception, Guid userId);
}

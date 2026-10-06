namespace buddy.Features.Guardians;

// Who can see a child, and why -- see docs/backend/observability.md. IDs only: never an email
// address, name, username or password.
internal static partial class GuardiansLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Guardian {GuardianId} created child account {ChildId}")]
    public static partial void ChildCreated(this ILogger logger, Guid childId, Guid guardianId);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Guardian invite {InviteId} ({Kind}) for child {ChildId} sent by {UserId}")]
    public static partial void GuardianInviteSent(this ILogger logger, Guid inviteId, GuardianKind kind, Guid childId, Guid userId);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Guardian invite {InviteId} accepted by {UserId}: now a guardian of child {ChildId}")]
    public static partial void GuardianInviteAccepted(this ILogger logger, Guid inviteId, Guid userId, Guid childId);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "Guardian invite {InviteId} refused for {UserId}: the account's email is unverified or isn't the invited address")]
    public static partial void GuardianInviteEmailMismatch(this ILogger logger, Guid inviteId, Guid userId);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "Guardian invite {InviteId} for child {ChildId} revoked by {UserId}")]
    public static partial void GuardianInviteRevoked(this ILogger logger, Guid inviteId, Guid childId, Guid userId);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Information, Message = "Guardian {GuardianId} gave up their link to child {ChildId}")]
    public static partial void GuardianLinkRevoked(this ILogger logger, Guid guardianId, Guid childId);

    [LoggerMessage(EventId = 2007, Level = LogLevel.Error, Message = "Keycloak admin request '{Operation}' failed with status {StatusCode}")]
    public static partial void KeycloakAdminRequestFailed(this ILogger logger, string operation, int statusCode);
}

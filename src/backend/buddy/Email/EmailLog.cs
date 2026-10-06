namespace buddy.Email;

// Outgoing mail -- see docs/backend/observability.md. Never the recipient's address or the
// message, which carries a one-time link.
internal static partial class EmailLog
{
    [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "Sent {EmailKind} email")]
    public static partial void EmailSent(this ILogger logger, string emailKind);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Error, Message = "Sending {EmailKind} email through {SmtpHost}:{SmtpPort} failed")]
    public static partial void EmailSendFailed(this ILogger logger, Exception exception, string emailKind, string smtpHost, int smtpPort);
}

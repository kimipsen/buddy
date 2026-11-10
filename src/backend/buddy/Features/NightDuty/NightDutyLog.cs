namespace buddy.Features.NightDuty;

// Night feed tokens -- see docs/backend/observability.md. Never the token itself.
internal static partial class NightDutyLog
{
    [LoggerMessage(EventId = 14001, Level = LogLevel.Information, Message = "Night duty iCal feed token {TokenId} for child {ChildId} issued by {UserId}")]
    public static partial void NightIcalTokenIssued(this ILogger logger, Guid tokenId, Guid childId, Guid userId);

    [LoggerMessage(EventId = 14002, Level = LogLevel.Information, Message = "Night duty iCal feed token {TokenId} revoked by {UserId}")]
    public static partial void NightIcalTokenRevoked(this ILogger logger, Guid tokenId, Guid userId);
}

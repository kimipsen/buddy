namespace buddy.Features.Pickups;

// Pickup feed tokens -- see docs/backend/observability.md. Never the token itself.
internal static partial class PickupsLog
{
    [LoggerMessage(EventId = 13001, Level = LogLevel.Information, Message = "Pickup iCal feed token {TokenId} issued by {UserId}")]
    public static partial void PickupIcalTokenIssued(this ILogger logger, Guid tokenId, Guid userId);

    [LoggerMessage(EventId = 13002, Level = LogLevel.Information, Message = "Pickup iCal feed token {TokenId} revoked by {UserId}")]
    public static partial void PickupIcalTokenRevoked(this ILogger logger, Guid tokenId, Guid userId);
}

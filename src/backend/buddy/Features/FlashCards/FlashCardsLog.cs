namespace buddy.Features.FlashCards;

// Finished flash-card games -- see docs/backend/observability.md. IDs only: never card text.
internal static partial class FlashCardsLog
{
    [LoggerMessage(EventId = 16001, Level = LogLevel.Information, Message = "Child {ChildId} finished flash-card game {GameId} (set {SetId}, tier {TierId}): {Stars} stars, outcome {Outcome}")]
    public static partial void GameFinished(this ILogger logger, Guid childId, Guid gameId, Guid setId, Guid tierId, int stars, string outcome);

    [LoggerMessage(EventId = 16002, Level = LogLevel.Warning, Message = "Flash-card game {GameId} of child {ChildId} was recovered: its stars were already awarded but the history entry was missing")]
    public static partial void GameRecovered(this ILogger logger, Guid childId, Guid gameId);
}

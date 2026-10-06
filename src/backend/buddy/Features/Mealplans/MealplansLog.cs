using System.Net;

namespace buddy.Features.Mealplans;

// AI provider keys and failures, and meal plan feed tokens -- see docs/backend/observability.md.
// Never an API key, a provider's response body, or what the family wrote to the assistant.
internal static partial class MealplansLog
{
    [LoggerMessage(EventId = 6001, Level = LogLevel.Information, Message = "{Provider} API key for child {ChildId}'s family set by {UserId}")]
    public static partial void AiProviderKeySet(this ILogger logger, AiProvider provider, Guid childId, Guid userId);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Information, Message = "{Provider} API key for child {ChildId}'s family removed by {UserId}")]
    public static partial void AiProviderKeyRemoved(this ILogger logger, AiProvider provider, Guid childId, Guid userId);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Information, Message = "Active AI provider for child {ChildId}'s family set to {Provider} by {UserId}")]
    public static partial void AiActiveProviderChanged(this ILogger logger, Guid childId, AiProvider provider, Guid userId);

    [LoggerMessage(EventId = 6004, Level = LogLevel.Warning, Message = "{Provider} request for AI session {SessionId} failed with status {StatusCode}")]
    public static partial void AiProviderRequestFailed(this ILogger logger, Exception exception, AiProvider provider, Guid sessionId, HttpStatusCode statusCode);

    [LoggerMessage(EventId = 6005, Level = LogLevel.Warning, Message = "AI provider {Provider} is stored for a family but not supported by this build")]
    public static partial void AiProviderUnsupported(this ILogger logger, Exception exception, AiProvider provider);

    [LoggerMessage(EventId = 6006, Level = LogLevel.Information, Message = "{Provider} connection test by {UserId} failed with status {StatusCode}")]
    public static partial void AiProviderConnectionTestFailed(this ILogger logger, AiProvider provider, Guid userId, HttpStatusCode statusCode);

    [LoggerMessage(EventId = 6007, Level = LogLevel.Information, Message = "iCal feed token {TokenId} for meal plan {MealPlanId} issued by {UserId}")]
    public static partial void MealPlanIcalTokenIssued(this ILogger logger, Guid tokenId, Guid mealPlanId, Guid userId);

    [LoggerMessage(EventId = 6008, Level = LogLevel.Information, Message = "iCal feed token {TokenId} for meal plan {MealPlanId} revoked by {UserId}")]
    public static partial void MealPlanIcalTokenRevoked(this ILogger logger, Guid tokenId, Guid mealPlanId, Guid userId);
}

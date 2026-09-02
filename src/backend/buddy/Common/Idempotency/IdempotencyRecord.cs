namespace buddy.Common.Idempotency;

public enum IdempotencyStatus
{
    InProgress,
    Completed
}

// One row per (UserId, Idempotency-Key) pair a client has sent on a POST. Id is the composite
// key itself, so two requests racing to claim the same key have their Insert resolved by
// Postgres's primary key -- the same "lose the race, return what won" contract
// MartenUserEventStore.CreateAsync already uses for KeycloakIdentity.
public sealed record IdempotencyRecord(
    string Id,
    Guid UserId,
    string Key,
    string RequestFingerprint,
    IdempotencyStatus Status,
    int? ResponseStatusCode,
    string? ResponseContentType,
    byte[]? ResponseBody,
    DateTimeOffset CreatedAt)
{
    public static string BuildId(Guid userId, string key) => $"{userId:N}:{key}";
}

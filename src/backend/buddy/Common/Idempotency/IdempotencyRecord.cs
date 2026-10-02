namespace buddy.Common.Idempotency;

// One row per (UserId, Idempotency-Key) pair a client has sent on a POST. Id is the composite
// key itself, so two requests racing to claim the same key have their Insert resolved by
// Postgres's primary key -- the same "lose the race, return what won" contract
// MartenUserEventStore.CreateAsync already uses for KeycloakIdentity.
public sealed record IdempotencyRecord(
    string Id,
    Guid UserId,
    string Key,
    string RequestFingerprint,
    // Null while the first request with this key is still running; set once it has a response.
    CompletedResponse? Response,
    DateTimeOffset CreatedAt)
{
    public static string BuildId(Guid userId, string key) => $"{userId:N}:{key}";
}

// The stored response a retry replays. ContentType is null for a response without a body (a 204).
public sealed record CompletedResponse(int StatusCode, string? ContentType, byte[] Body);

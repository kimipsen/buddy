using buddy.Features.Users;

namespace buddy.Features.Feelings;

public union FeelingsShareTokenEvent(
    FeelingsShareTokenCreated,
    FeelingsShareTokenRevoked
)
{
    public static FeelingsShareTokenEvent FromPayload(object payload) => payload switch
    {
        FeelingsShareTokenCreated e => e,
        FeelingsShareTokenRevoked e => e,
        _ => throw new ArgumentException($"Unknown feelings share token event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        FeelingsShareTokenCreated => nameof(FeelingsShareTokenCreated),
        FeelingsShareTokenRevoked => nameof(FeelingsShareTokenRevoked),
    };
}

// TokenHash is the SHA-256 of the plaintext token, never the token itself -- same as SleepDiaryShareTokenCreated.
public sealed record FeelingsShareTokenCreated(
    FeelingsShareTokenId Id,
    UserId ChildId,
    string TokenHash,
    UserId CreatedBy,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset OccurredAt);

public sealed record FeelingsShareTokenRevoked(FeelingsShareTokenId Id, UserId ModifiedBy, DateTimeOffset OccurredAt);

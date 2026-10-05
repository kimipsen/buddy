using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

public union SleepDiaryShareTokenEvent(
    SleepDiaryShareTokenCreated,
    SleepDiaryShareTokenRevoked
)
{
    public static SleepDiaryShareTokenEvent FromPayload(object payload) => payload switch
    {
        SleepDiaryShareTokenCreated e => e,
        SleepDiaryShareTokenRevoked e => e,
        _ => throw new ArgumentException($"Unknown sleep diary share token event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        SleepDiaryShareTokenCreated => nameof(SleepDiaryShareTokenCreated),
        SleepDiaryShareTokenRevoked => nameof(SleepDiaryShareTokenRevoked),
    };
}

// TokenHash is the SHA-256 of the plaintext token, never the token itself -- same as IcalTokenIssued.
public sealed record SleepDiaryShareTokenCreated(
    SleepDiaryShareTokenId Id,
    UserId ChildId,
    string TokenHash,
    UserId CreatedBy,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset OccurredAt);

public sealed record SleepDiaryShareTokenRevoked(SleepDiaryShareTokenId Id, UserId ModifiedBy, DateTimeOffset OccurredAt);

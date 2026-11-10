using buddy.Features.Users;

namespace buddy.Features.NightDuty;

public union NightFeedEvent(
    NightFeedStarted,
    NightIcalTokenIssued,
    NightIcalTokenRevoked
)
{
    public static NightFeedEvent FromPayload(object payload) => payload switch
    {
        NightFeedStarted e => e,
        NightIcalTokenIssued e => e,
        NightIcalTokenRevoked e => e,
        _ => throw new ArgumentException($"Unknown night feed event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        NightFeedStarted => nameof(NightFeedStarted),
        NightIcalTokenIssued => nameof(NightIcalTokenIssued),
        NightIcalTokenRevoked => nameof(NightIcalTokenRevoked),
    };
}

// Appended lazily together with the guardian's first NightIcalTokenIssued, never on its own.
public sealed record NightFeedStarted(NightFeedId Id, UserId GuardianId, DateTimeOffset OccurredAt);

// No IssuedBy/RevokedBy: only the feed's own guardian can write the stream. ChildId is the child
// whose nights the token shows.
public sealed record NightIcalTokenIssued(NightFeedId Id, IcalTokenId TokenId, UserId ChildId, string Hash, DateTimeOffset OccurredAt);

public sealed record NightIcalTokenRevoked(NightFeedId Id, IcalTokenId TokenId, DateTimeOffset OccurredAt);

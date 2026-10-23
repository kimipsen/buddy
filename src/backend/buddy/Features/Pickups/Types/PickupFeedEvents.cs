using buddy.Features.Users;

namespace buddy.Features.Pickups;

public union PickupFeedEvent(
    PickupFeedStarted,
    PickupIcalTokenIssued,
    PickupIcalTokenRevoked
)
{
    public static PickupFeedEvent FromPayload(object payload) => payload switch
    {
        PickupFeedStarted e => e,
        PickupIcalTokenIssued e => e,
        PickupIcalTokenRevoked e => e,
        _ => throw new ArgumentException($"Unknown pickup feed event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        PickupFeedStarted => nameof(PickupFeedStarted),
        PickupIcalTokenIssued => nameof(PickupIcalTokenIssued),
        PickupIcalTokenRevoked => nameof(PickupIcalTokenRevoked),
    };
}

// Appended lazily together with the guardian's first PickupIcalTokenIssued, never on its own.
public sealed record PickupFeedStarted(PickupFeedId Id, UserId GuardianId, DateTimeOffset OccurredAt);

// No IssuedBy/RevokedBy: only the feed's own guardian can write the stream.
public sealed record PickupIcalTokenIssued(PickupFeedId Id, IcalTokenId TokenId, string Hash, DateTimeOffset OccurredAt);

public sealed record PickupIcalTokenRevoked(PickupFeedId Id, IcalTokenId TokenId, DateTimeOffset OccurredAt);

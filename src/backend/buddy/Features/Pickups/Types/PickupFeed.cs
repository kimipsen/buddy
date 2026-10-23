using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.Pickups;

// One stream per guardian: the iCal subscription tokens for "the pickups and drop-offs I am
// assigned to", across all of the guardian's children. Separate from PickupSchedule, which is one
// stream per child. See docs/backend/analysis/pickup-ical-feed.md, Question 1.
public sealed record PickupFeed(
    PickupFeedId Id,
    UserId GuardianId,
    ImmutableDictionary<IcalTokenId, IcalTokenInfo> Tokens)
{
    // Constant-time per candidate, mirroring MealPlan.FindMatchingToken -- the caller supplies an
    // already-hashed value so the plaintext token is never compared or logged here.
    public IcalTokenId? FindMatchingToken(string submittedTokenHash)
    {
        var submittedBytes = Encoding.UTF8.GetBytes(submittedTokenHash);

        foreach (var (id, info) in Tokens)
        {
            if (CryptographicOperations.FixedTimeEquals(submittedBytes, Encoding.UTF8.GetBytes(info.Hash)))
            {
                return id;
            }
        }

        return null;
    }

    public static PickupFeed? Rehydrate(IEnumerable<PickupFeedEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static PickupFeed Replay(IEnumerable<PickupFeedEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Not named Apply/Create -- see the comment on PickupSchedule.Start.
    public static PickupFeed Start(PickupFeedEvent @event) => @event switch
    {
        PickupFeedStarted started => new PickupFeed(
            started.Id,
            started.GuardianId,
            ImmutableDictionary<IcalTokenId, IcalTokenInfo>.Empty),
        _ => throw EventReplay.NotAStartEvent(nameof(PickupFeed), @event.EventType)
    };

    public static PickupFeed Advance(PickupFeed feed, PickupFeedEvent @event) => @event switch
    {
        PickupIcalTokenIssued issued => feed with { Tokens = feed.Tokens.SetItem(issued.TokenId, new IcalTokenInfo(issued.Hash, issued.OccurredAt)) },
        PickupIcalTokenRevoked revoked => feed with { Tokens = feed.Tokens.Remove(revoked.TokenId) },
        PickupFeedStarted => throw EventReplay.AlreadyStarted(nameof(PickupFeed), @event.EventType)
    };
}

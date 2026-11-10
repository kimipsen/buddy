using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// One stream per guardian: the iCal subscription tokens for their children's night schedules, each
// token naming one child. Separate from NightSchedule, which is one stream per child and shared by
// every guardian of that child. See docs/backend/analysis/night-duty-ical-feed.md, Question 1.
public sealed record NightFeed(
    NightFeedId Id,
    UserId GuardianId,
    ImmutableDictionary<IcalTokenId, NightIcalTokenInfo> Tokens)
{
    // Constant-time per candidate, mirroring PickupFeed.FindMatchingToken -- the caller supplies an
    // already-hashed value so the plaintext token is never compared or logged here.
    public NightIcalTokenInfo? FindMatchingToken(string submittedTokenHash)
    {
        var submittedBytes = Encoding.UTF8.GetBytes(submittedTokenHash);

        return Tokens.Values.FirstOrDefault(
            info => CryptographicOperations.FixedTimeEquals(submittedBytes, Encoding.UTF8.GetBytes(info.Hash)));
    }

    public static NightFeed? Rehydrate(IEnumerable<NightFeedEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static NightFeed Replay(IEnumerable<NightFeedEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Not named Apply/Create -- see the comment on WorkLocationSchedule.Start.
    public static NightFeed Start(NightFeedEvent @event) => @event switch
    {
        NightFeedStarted started => new NightFeed(
            started.Id,
            started.GuardianId,
            ImmutableDictionary<IcalTokenId, NightIcalTokenInfo>.Empty),
        _ => throw EventReplay.NotAStartEvent(nameof(NightFeed), @event.EventType)
    };

    public static NightFeed Advance(NightFeed feed, NightFeedEvent @event) => @event switch
    {
        NightIcalTokenIssued issued => feed with
        {
            Tokens = feed.Tokens.SetItem(issued.TokenId, new NightIcalTokenInfo(issued.ChildId, issued.Hash, issued.OccurredAt))
        },
        NightIcalTokenRevoked revoked => feed with { Tokens = feed.Tokens.Remove(revoked.TokenId) },
        NightFeedStarted => throw EventReplay.AlreadyStarted(nameof(NightFeed), @event.EventType)
    };
}

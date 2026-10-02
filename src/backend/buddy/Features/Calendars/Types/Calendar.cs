using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record Calendar(
    CalendarId Id,
    string Name,
    Icon Icon,
    TimeZoneId TimeZoneId,
    CalendarOwner Owner,
    ImmutableDictionary<UserId, CalendarRole> Members,
    ImmutableDictionary<IcalTokenId, IcalTokenInfo> Tokens,
    bool IsDeleted = false)
{
    // Assumed for every calendar until a CalendarIconChanged event first appears in its stream --
    // CalendarCreated/CalendarCreatedForGroup never carry an icon themselves (see CalendarEvents.cs).
    public static readonly Icon DefaultIcon = new("📅");

    // Constant-time per candidate, mirroring VerifyEmailHandler's token comparison -- the caller
    // supplies an already-hashed value so the plaintext token is never compared or logged here.
    public IcalTokenId? FindMatchingToken(string submittedTokenHash)
    {
        if (IsDeleted)
        {
            return null;
        }

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

    public static Calendar? Rehydrate(IEnumerable<CalendarEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static Calendar Replay(IEnumerable<CalendarEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so CalendarSnapshotProjection
    // can drive the same logic one Marten-delivered event at a time instead of duplicating this
    // switch. Deliberately not named Apply/Create -- those names are a convention JasperFx's
    // projection source generator scans for on any type used as a projection document, and Calendar
    // is that document (see Question 4/5 in docs/backend/analysis/event-stream-snapshots.md).
    public static Calendar Start(CalendarEvent @event) => @event switch
    {
        CalendarCreated created => new Calendar(
            created.CalendarId,
            created.Name,
            DefaultIcon,
            created.TimeZoneId,
            new CalendarOwner.User(created.OwnerId),
            ImmutableDictionary<UserId, CalendarRole>.Empty.Add(created.OwnerId, CalendarRole.Owner),
            ImmutableDictionary<IcalTokenId, IcalTokenInfo>.Empty),
        CalendarCreatedForGroup created => new Calendar(
            created.CalendarId,
            created.Name,
            DefaultIcon,
            created.TimeZoneId,
            new CalendarOwner.Group(created.OwnerId),
            ImmutableDictionary<UserId, CalendarRole>.Empty,
            ImmutableDictionary<IcalTokenId, IcalTokenInfo>.Empty),
        _ => throw EventReplay.NotAStartEvent(nameof(Calendar), @event.EventType)
    };

    public static Calendar Advance(Calendar calendar, CalendarEvent @event) => @event switch
    {
        CalendarIconChanged changed => calendar with { Icon = changed.Icon },
        CalendarTransferredToGroup transferred => calendar with { Owner = new CalendarOwner.Group(transferred.NewGroupId) },
        MemberRoleGranted granted => calendar with { Members = calendar.Members.SetItem(granted.MemberId, granted.Role) },
        MemberRoleRevoked revoked => calendar with { Members = calendar.Members.Remove(revoked.MemberId) },
        IcalTokenIssued issued => calendar with { Tokens = calendar.Tokens.SetItem(issued.TokenId, new IcalTokenInfo(issued.TokenHash, issued.OccurredAt)) },
        IcalTokenRevoked revoked => calendar with { Tokens = calendar.Tokens.Remove(revoked.TokenId) },
        CalendarDeleted => calendar with { IsDeleted = true },
        CalendarCreated or CalendarCreatedForGroup => throw EventReplay.AlreadyStarted(nameof(Calendar), @event.EventType)
    };
}

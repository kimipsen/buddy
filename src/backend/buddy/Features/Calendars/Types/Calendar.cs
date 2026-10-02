using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

using buddy.Common.Aggregates;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record Calendar(
    CalendarId Id,
    string Name,
    Icon Icon,
    TimeZoneId TimeZoneId,
    // The owning group -- set at creation, changed only by CalendarTransferredToGroup. Ownership is
    // anchored to the group as a whole; per-user roles come from its CalendarPermissionPolicy (see
    // CalendarAuthorization), with Members as explicit overrides.
    GroupId GroupId,
    ImmutableDictionary<UserId, CalendarRole> Members,
    ImmutableDictionary<IcalTokenId, IcalTokenInfo> Tokens,
    bool IsDeleted = false)
{
    // What CreateCalendar stores on CalendarCreatedForGroup when the request gives no icon.
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
        CalendarCreatedForGroup created => new Calendar(
            created.CalendarId,
            created.Name,
            created.Icon,
            created.TimeZoneId,
            created.GroupId,
            ImmutableDictionary<UserId, CalendarRole>.Empty,
            ImmutableDictionary<IcalTokenId, IcalTokenInfo>.Empty),
        _ => throw EventReplay.NotAStartEvent(nameof(Calendar), @event.EventType)
    };

    public static Calendar Advance(Calendar calendar, CalendarEvent @event) => @event switch
    {
        CalendarIconChanged changed => calendar with { Icon = changed.Icon },
        CalendarTransferredToGroup transferred => calendar with { GroupId = transferred.NewGroupId },
        MemberRoleGranted granted => calendar with { Members = calendar.Members.SetItem(granted.MemberId, granted.Role) },
        MemberRoleRevoked revoked => calendar with { Members = calendar.Members.Remove(revoked.MemberId) },
        IcalTokenIssued issued => calendar with { Tokens = calendar.Tokens.SetItem(issued.TokenId, new IcalTokenInfo(issued.TokenHash, issued.OccurredAt)) },
        IcalTokenRevoked revoked => calendar with { Tokens = calendar.Tokens.Remove(revoked.TokenId) },
        CalendarDeleted => calendar with { IsDeleted = true },
        CalendarCreatedForGroup => throw EventReplay.AlreadyStarted(nameof(Calendar), @event.EventType)
    };
}

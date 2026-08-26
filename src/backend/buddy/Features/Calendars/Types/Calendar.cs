using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

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

    public static Calendar? Rehydrate(IEnumerable<CalendarEvent> events)
    {
        Calendar? calendar = null;

        foreach (var @event in events)
        {
            calendar = @event switch
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
                CalendarIconChanged changed => calendar! with { Icon = changed.Icon },
                CalendarTransferredToGroup transferred => calendar! with { Owner = new CalendarOwner.Group(transferred.NewGroupId) },
                MemberRoleGranted granted => calendar! with { Members = calendar!.Members.SetItem(granted.MemberId, granted.Role) },
                MemberRoleRevoked revoked => calendar! with { Members = calendar!.Members.Remove(revoked.MemberId) },
                IcalTokenIssued issued => calendar! with { Tokens = calendar!.Tokens.SetItem(issued.TokenId, new IcalTokenInfo(issued.TokenHash, issued.OccurredAt)) },
                IcalTokenRevoked revoked => calendar! with { Tokens = calendar!.Tokens.Remove(revoked.TokenId) },
                CalendarDeleted => calendar! with { IsDeleted = true },
                _ => calendar
            };
        }

        return calendar;
    }
}

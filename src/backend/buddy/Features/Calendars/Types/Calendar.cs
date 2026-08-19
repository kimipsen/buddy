using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

using buddy.Features.Users;

namespace buddy.Features.Calendars;

public sealed record Calendar(
    CalendarId Id,
    string Name,
    TimeZoneId TimeZoneId,
    ImmutableDictionary<UserId, CalendarRole> Members,
    ImmutableDictionary<IcalTokenId, IcalTokenInfo> Tokens,
    bool IsDeleted = false)
{
    public bool CanView(UserId userId) => !IsDeleted && Members.ContainsKey(userId);

    public bool CanContribute(UserId userId) =>
        !IsDeleted && Members.TryGetValue(userId, out var role) && role is CalendarRole.Owner or CalendarRole.Contributor;

    public bool IsOwner(UserId userId) =>
        !IsDeleted && Members.TryGetValue(userId, out var role) && role == CalendarRole.Owner;

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
                    created.TimeZoneId,
                    ImmutableDictionary<UserId, CalendarRole>.Empty.Add(created.OwnerId, CalendarRole.Owner),
                    ImmutableDictionary<IcalTokenId, IcalTokenInfo>.Empty),
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

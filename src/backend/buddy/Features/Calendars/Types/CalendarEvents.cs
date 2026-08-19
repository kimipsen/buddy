using buddy.Features.Users;

namespace buddy.Features.Calendars;

public union CalendarEvent(
    CalendarCreated,
    CalendarDeleted,
    MemberRoleGranted,
    MemberRoleRevoked,
    IcalTokenIssued,
    IcalTokenRevoked
)
{
    public static CalendarEvent FromPayload(object payload) => payload switch
    {
        CalendarCreated e => e,
        CalendarDeleted e => e,
        MemberRoleGranted e => e,
        MemberRoleRevoked e => e,
        IcalTokenIssued e => e,
        IcalTokenRevoked e => e,
        _ => throw new ArgumentException($"Unknown calendar event payload: {payload.GetType().Name}", nameof(payload)),
    };

    // Persistence/API discriminator. A union is a value type, so GetType().Name on a boxed
    // CalendarEvent returns "CalendarEvent" for every case -- use this instead.
    public string EventType => this switch
    {
        CalendarCreated => nameof(CalendarCreated),
        CalendarDeleted => nameof(CalendarDeleted),
        MemberRoleGranted => nameof(MemberRoleGranted),
        MemberRoleRevoked => nameof(MemberRoleRevoked),
        IcalTokenIssued => nameof(IcalTokenIssued),
        IcalTokenRevoked => nameof(IcalTokenRevoked),
    };
}

public sealed record CalendarCreated(CalendarId CalendarId, UserId OwnerId, string Name, TimeZoneId TimeZoneId, DateTimeOffset OccurredAt);

public sealed record CalendarDeleted(CalendarId CalendarId, UserId DeletedBy, DateTimeOffset OccurredAt);

// Role is always Contributor or Viewer -- Owner is assigned only by CalendarCreated.
public sealed record MemberRoleGranted(CalendarId CalendarId, UserId MemberId, CalendarRole Role, UserId GrantedBy, DateTimeOffset OccurredAt);

public sealed record MemberRoleRevoked(CalendarId CalendarId, UserId MemberId, UserId RevokedBy, DateTimeOffset OccurredAt);

// TokenHash is a SHA-256 hash of the plaintext token, never the token itself -- same reasoning as
// User's EmailVerificationRequested: the event stream is append-only, so a bare secret in it
// could never be revoked or purged.
public sealed record IcalTokenIssued(CalendarId CalendarId, IcalTokenId TokenId, string TokenHash, UserId IssuedBy, DateTimeOffset OccurredAt);

public sealed record IcalTokenRevoked(CalendarId CalendarId, IcalTokenId TokenId, UserId RevokedBy, DateTimeOffset OccurredAt);

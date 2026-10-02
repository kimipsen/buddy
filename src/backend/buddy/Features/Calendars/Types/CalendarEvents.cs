using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public union CalendarEvent(
    CalendarCreatedForGroup,
    CalendarIconChanged,
    CalendarTransferredToGroup,
    CalendarDeleted,
    MemberRoleGranted,
    MemberRoleRevoked,
    IcalTokenIssued,
    IcalTokenRevoked
)
{
    public static CalendarEvent FromPayload(object payload) => payload switch
    {
        CalendarCreatedForGroup e => e,
        CalendarIconChanged e => e,
        CalendarTransferredToGroup e => e,
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
        CalendarCreatedForGroup => nameof(CalendarCreatedForGroup),
        CalendarIconChanged => nameof(CalendarIconChanged),
        CalendarTransferredToGroup => nameof(CalendarTransferredToGroup),
        CalendarDeleted => nameof(CalendarDeleted),
        MemberRoleGranted => nameof(MemberRoleGranted),
        MemberRoleRevoked => nameof(MemberRoleRevoked),
        IcalTokenIssued => nameof(IcalTokenIssued),
        IcalTokenRevoked => nameof(IcalTokenRevoked),
    };
}

// Every calendar is group-owned (see docs/backend/analysis/group-owned-calendars-and-permissions.md);
// the creating request's icon (Calendar.DefaultIcon when it gave none) is stored on the event itself.
public sealed record CalendarCreatedForGroup(CalendarId CalendarId, GroupId GroupId, string Name, Icon Icon, TimeZoneId TimeZoneId, DateTimeOffset OccurredAt);

// Icon is the only calendar-level detail that can change after creation today -- Name and
// TimeZoneId remain fixed.
public sealed record CalendarIconChanged(CalendarId CalendarId, Icon Icon, UserId ChangedBy, DateTimeOffset OccurredAt);

// The one exception to "ownership is fixed at creation, never transferred" -- a calendar's
// owning group can be moved to another group afterward, gated by CheckOwner on the calendar itself and
// GroupAuthorization.CheckManage on NewGroupId (two-sided consent, the same shape
// ShareMealPlanWithGroup/ShareMedicineWithGroup already use). Calendar.Members is untouched by a
// transfer -- explicit per-user grants always win over the owning group's policy regardless of
// which group that is, so nobody's access silently changes just because ownership moved.
public sealed record CalendarTransferredToGroup(CalendarId CalendarId, GroupId NewGroupId, UserId TransferredBy, DateTimeOffset OccurredAt);

public sealed record CalendarDeleted(CalendarId CalendarId, UserId DeletedBy, DateTimeOffset OccurredAt);

// Role is always Contributor or Viewer -- Owner comes only from the owning group's policy.
public sealed record MemberRoleGranted(CalendarId CalendarId, UserId MemberId, CalendarRole Role, UserId GrantedBy, DateTimeOffset OccurredAt);

public sealed record MemberRoleRevoked(CalendarId CalendarId, UserId MemberId, UserId RevokedBy, DateTimeOffset OccurredAt);

// TokenHash is a SHA-256 hash of the plaintext token, never the token itself -- same reasoning as
// User's EmailVerificationRequested: the event stream is append-only, so a bare secret in it
// could never be revoked or purged.
public sealed record IcalTokenIssued(CalendarId CalendarId, IcalTokenId TokenId, string TokenHash, UserId IssuedBy, DateTimeOffset OccurredAt);

public sealed record IcalTokenRevoked(CalendarId CalendarId, IcalTokenId TokenId, UserId RevokedBy, DateTimeOffset OccurredAt);
